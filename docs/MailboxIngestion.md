# Automated Report Pickup

SecurityRecap can watch a mailbox for the nightly patrol report, ingest the PDF attachment,
compare it against the property's history, and email the generated summary to the board.
This replaces the Power Automate flow plus the manual upload step.

## What it does on each poll

1. Lists messages in the configured folder that have attachments, arrived since the last one
   it saw, and match the sender/subject filters.
2. Downloads the matching PDF attachments.
3. Runs each PDF through the normal ingestion pipeline (`IIngestionService`) — the same path
   the manual **Upload PDF** button uses. Claude receives the PDF plus 90 days of the
   property's history: incident counts by type over 7/30/90 days, repeat addresses, repeat
   vehicles, average incidents per report, and days since the last high/urgent incident.
4. Stores the report, incidents, vehicles, violations and addresses of interest, and writes
   the Markdown summary to storage as the report's summary file.
5. Sends the summary email (if enabled), with the Markdown summary attached.
6. Marks the message read and/or moves it, per configuration.

Ingestion is idempotent per attachment: the report's `external_id` is
`graph:{internetMessageId}:{fileName}`, so re-processing the same message is a no-op.
That means a poll that runs twice, or overlaps a manual **Check now**, cannot duplicate a report.

## Entra app registration

App-only (client credentials) access, so nothing depends on a signed-in user.

1. **Entra admin center → App registrations → New registration.** Single tenant. No redirect URI.
2. Note the **Directory (tenant) ID** and **Application (client) ID**.
3. **Certificates & secrets → New client secret.** Copy the *value* immediately — it is
   shown once. Note its expiry; the poll starts failing when it lapses.
4. **API permissions → Add a permission → Microsoft Graph → Application permissions:**
   - `Mail.Read` — required.
   - `Mail.Send` — only if the summary email is enabled.

   Then **Grant admin consent**. No directory permissions are needed: **Test connection**
   reads the configured mail folder, so it exercises the same permission polling uses.

### Shared mailbox vs. licensed mailbox

Use a **shared mailbox**. App-only Graph access does not care whether the mailbox is licensed,
and a shared mailbox is free, has no password to rotate, and has sign-in blocked on its
underlying account — which is exactly what you want for something only a daemon reads.

- 50 GB without a license. Only add an Exchange Online Plan 2 license if you exceed that or
  need archiving / litigation hold.
- Sign-in being blocked on the account is irrelevant here. It would only matter for delegated
  or password-based access (IMAP, ROPC); client credentials do not sign a user in.
- A **distribution list will not work** — it has no mailbox to read. It must be a shared or
  user mailbox.
- If the summary email is enabled, it is sent *from* this mailbox and lands in its Sent Items,
  so pick an address the board will recognise as the sender.

Have the security vendor deliver the report to this address (or add it as an extra recipient
alongside the current one during changeover).

### Restrict the app to one mailbox

`Mail.Read` as an application permission grants access to *every* mailbox in the tenant by
default — including yours. Scope it down before going live. The simplest option is an
application access policy (Exchange Online PowerShell); `-PolicyScopeGroupId` accepts the
shared mailbox directly, though pointing it at a mail-enabled security group makes adding a
second property later a group membership change rather than a new policy:

```powershell
New-ApplicationAccessPolicy `
  -AppId <application-client-id> `
  -PolicyScopeGroupId reports@example.com `
  -AccessRight RestrictAccess `
  -Description "SecurityRecap patrol report pickup"
```

Verify it took effect:

```powershell
Test-ApplicationAccessPolicy -Identity reports@example.com -AppId <application-client-id>
```

Policy changes can take up to an hour to propagate.

Exchange Online also offers **RBAC for Applications**, which scopes app permissions more
granularly (and is what Microsoft now steers people toward). Application access policies still
work and are less setup; either is fine for a single mailbox.

## Configure the property

**Settings → Automated Report Pickup** (admin only). Pick the property, then fill in:

| Field | Notes |
| --- | --- |
| Directory / Application ID | From the app registration. |
| Client secret | Encrypted before storage and never returned to the browser. Leave blank on a later save to keep the stored value. |
| Mailbox address | The mailbox the report is delivered to. |
| Folder | Well-known name (`inbox`, `archive`) or a folder id. |
| From address / Subject contains / Attachment name contains | Optional filters. Narrow enough that only the patrol report matches. |
| Check every | Per-property cadence in minutes. |
| First-run lookback | How far back to reach on the very first check, so it does not backfill years of mail. |
| Move processed mail to | Optional destination folder. |
| Summary email | Recipients and subject prefix. The report date is appended to the prefix. |

Then use **Test connection** to confirm credentials and mailbox access, and **Check now** to
run a poll immediately. The per-message results table shows what matched and what happened.

## Operations

- The background poller wakes on a fixed tick (`MailboxIngest:TickIntervalSeconds`, default
  60s) and polls each property whose own interval has elapsed.
- After a failure the property backs off exponentially (2× per consecutive failure, capped at
  6 hours) so a bad credential does not hammer Graph. A successful poll, or saving a new
  client secret, resets the backoff.
- Failures are recorded on the config row and shown in the UI as **Last error** with a
  consecutive-failure count.
- `MailboxIngest:Enabled: false` stops all automatic polling; **Check now** still works.

## Deployment prerequisites

Two settings in `appsettings.Production.json` must point **outside** the deploy root, because
`deploy.bat` moves the whole application directory on every release:

- `DataProtection:KeyPath` — the key ring that encrypts stored client secrets. If this is lost,
  every stored secret becomes undecryptable and has to be re-entered. The app logs a warning at
  startup when it is unset in a non-development environment.
- `LocalStorage:RootPath` — where report PDFs and summary files are written.

The app pool identity needs read/write on both paths.

### Database migration

`MailboxIngestConfig` adds the `mailbox_ingest_configs` table. Migrations run automatically
only in Development (`DbSeeder.SeedAsync`), so apply it to production explicitly:

```bash
dotnet ef database update -p src/SecurityRecap.Infrastructure -s src/SecurityRecap.Api
```

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| "Microsoft Entra rejected the app credentials" | Wrong tenant/client id, or an expired client secret. |
| "Microsoft Graph denied access to the mailbox" | Admin consent not granted, or an application access policy that excludes this mailbox. |
| "could not find that mailbox" | Address does not resolve to a mailbox in this tenant. A shared mailbox is fine; a distribution list is not (it has no mailbox to read). |
| "could not find that mail folder" | Folder name is wrong. Use a well-known name (`inbox`, `archive`) or a folder id. |
| Poll succeeds but ingests nothing | Filters too narrow, or the watermark has already passed the message. Widen the filters and re-check. |
| "The stored client secret could not be decrypted" | The Data Protection key ring was lost — usually `DataProtection:KeyPath` unset before a deploy. Re-enter the client secret. |

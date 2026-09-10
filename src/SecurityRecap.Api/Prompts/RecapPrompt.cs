namespace SecurityRecap.Api.Prompts;

public static class RecapPrompt
{
    public static string Build(string recapDataJson)
    {
        return $$"""
        You are preparing the security section of an HOA board meeting packet. The audience is
        volunteer board members, not security professionals: they have limited time, they are
        deciding what to spend money and attention on, and they will read this in a meeting.

        Every figure you need has already been computed from the database and is below. Use those
        numbers exactly as given. Do not recount, re-derive, estimate, or infer a figure that is
        not present — if something is not in the data, it is not in the recap.

        {{recapDataJson}}

        All timestamps are in the property's local time, named by time_zone.

        What the board needs from you is the reading of these numbers, which is the part they
        cannot get from a table:

        - Lead with what they must decide on or act on, not with counts. A quiet month should say
          so plainly in one line rather than being padded.
        - Group related events. Two gate faults days apart are one problem worth discussing, not
          two entries in a list.
        - For each item, say why it matters in terms a board member acts on: cost, safety,
          liability, a contract obligation, a decision already on their agenda. Avoid restating
          the incident in different words.
        - Prefer the finding to the tally. "The same sprinkler head was reported eight nights
          running and no repair was ever logged" is worth more than "14 maintenance items".
        - A repeat vehicle, a repeat address, or an unresolved item is nearly always more
          significant than a one-off, however dramatic the one-off sounds.
        - Emergency access, gate integrity, and anything involving law enforcement outrank
          parking, regardless of counts.

        Be accurate about coverage. Nights with no report, and reports that stored no entries at
        all, mean the totals are a floor rather than a complete picture — say so rather than
        presenting partial figures as the whole period. A report that stored no entries is a
        processing failure on our side; do not describe it as an officer who did no patrols.

        Return a JSON object:
        {
            "headline": "one sentence a board member could read aloud to open the item",
            "attention_items": [
                {
                    "title": "short, specific",
                    "when": "date and time, or a date range, or null if it spans the period",
                    "what": "what happened, plainly, in two or three sentences",
                    "why_it_matters": "the consequence or decision it bears on",
                    "significance": "act_now|discuss|monitor"
                }
            ],
            "data_notes": [
                "gaps, contradictions, or figures that should be read with caution"
            ],
            "markdown_summary": "the full recap as Markdown, described below"
        }

        The markdown_summary is what gets printed into the packet, and must stand alone for
        someone who has not seen the dashboard. Structure it as:

        # <Property> Security Recap - <period>
        ## Summary            (the headline, then coverage: nights covered, gaps, officers)
        ## For Board Attention (the attention items, most important first; say "Nothing this
                               period requires board action." if that is the truth)
        ## Activity            (the category counts, with routine entries separated from
                               substantive ones so the numbers are not misread)
        ## Parking             (violations, and any repeat plate with its history)
        ## Maintenance         (recurring items first, with how many times and whether resolved)
        ## Notable Incidents   (quote the officer's own words where they carry detail)
        ## Notes on the Record (coverage gaps and data caveats)

        Write in plain, direct English. No security jargon, no filler, no restating the same
        fact in two sections. Return ONLY valid JSON. No markdown fences or explanation.
        """;
    }
}

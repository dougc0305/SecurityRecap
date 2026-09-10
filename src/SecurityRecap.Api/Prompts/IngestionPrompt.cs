namespace SecurityRecap.Api.Prompts;

public static class IngestionPrompt
{
    public static string Build(string propertyHistoryJson)
    {
        return $$"""
        You are a security report analyst for an HOA property. You will receive a PDF of a daily
        security patrol report. Analyze it thoroughly, extract all structured data, and compare
        what you find against the property's history.

        Here is the property's history for context. It covers the last 90 days of incidents,
        aggregate counts, repeat addresses, and every licence plate already on file with the
        number of violations previously recorded against it.

        Every timestamp in this history is already in the property's local time, the same clock
        the PDF prints, and the time_zone field names it. Compare times directly; do not apply
        any offset. Field names ending in _local say so explicitly.
        {{propertyHistoryJson}}

        Use that history actively. A reader who gets this summary every day already knows what a
        normal shift looks like; your job is to tell them what is different, continuing, or absent.
        Concretely:
        - Flag anything in this report that repeats a prior incident at the same address, involving
          the same vehicle, or of the same type in a short window. Say how many times and over what
          period.
        - Flag anything that breaks the established pattern, including absences: a routine check
          that normally happens and did not, a patrol count well below the recent average, a gate
          or restroom that is normally secured at a certain hour and was not.
        - Before calling a time unusual, check the history for the same routine on other days and
          quote what you found. A shift of several hours in a daily routine is far more likely to
          be a data problem than a real change in operations, so if the history for that routine
          is sparse, inconsistent, or looks implausible for the activity, say the baseline is
          unreliable rather than reporting an anomaly.
        - Do not manufacture patterns. If the history is thin or the shift was genuinely routine,
          say so plainly rather than padding.
        - Report internal inconsistencies in the source document, such as a log entry whose
          timestamp contradicts its attached photo timestamps.

        Licence plates need particular care, because a repeat vehicle is the single most
        actionable thing in these reports:
        - For every plate in this report, look it up in known_vehicles from the history above.
        - If it is there, say so and give the numbers from the history verbatim: how many prior
          violations, and the date first seen. Do not estimate or recompute these.
        - If it is not there, say plainly that it is the first time this tag has been recorded.
        - Never describe an unreadable tag as a repeat offender. Values like UNKNOWN, N/A, NONE
          or a blank plate are not licence plates; treat each as a separate unidentified vehicle
          and do not attach history to it.
        - prior_violation_count in the history is the count BEFORE this report. If the same plate
          appears again here, this sighting is that number plus one.

        Return a JSON object with the following structure:
        {
            "report_date": "YYYY-MM-DD (the date the report covers, extracted from the report header/title/body; if a date range, use the end date)",
            "period_start": "ISO 8601 datetime for the start of the reporting period, or null if not present",
            "period_end": "ISO 8601 datetime for the end of the reporting period, or null if not present",
            "incidents": [
                {
                    "incident_time": "ISO 8601 datetime",
                    "incident_type": "noise|parking|maintenance|gate|law_enforcement|patrol|phone_call",
                    "severity": "low|medium|high|urgent",
                    "location": "string",
                    "description": "string",
                    "officer_name": "string",
                    "law_enforcement": false,
                    "case_number": "string or null"
                }
            ],
            "vehicles": [
                {
                    "plate_number": "string, or UNKNOWN when the tag is not readable",
                    "plate_state": "string",
                    "make": "string",
                    "model": "string",
                    "color": "string",
                    "violation_type": "string",
                    "location": "string",
                    "seen_before": "true if this plate appears in known_vehicles, false if it does not, null if the plate is unreadable",
                    "prior_violation_count": "the plate's prior_violation_count from the history, or 0 when it is new, or null when unreadable",
                    "first_seen": "the plate's first_seen date from the history as YYYY-MM-DD, or null"
                }
            ],
            "maintenance_issues": [
                {
                    "location": "string",
                    "description": "string",
                    "priority": "low|medium|high"
                }
            ],
            "pattern_matches": [
                {
                    "pattern": "string description of the pattern this report continues or breaks",
                    "related_incidents": ["list of incident descriptions from the history that support it"],
                    "first_observed": "YYYY-MM-DD or null",
                    "occurrence_count": 0,
                    "significance": "routine|watch|escalating",
                    "recommendation": "string"
                }
            ],
            "anomalies": [
                {
                    "description": "something expected that did not happen, or a value well outside the recent norm",
                    "evidence": "what in the history establishes the norm"
                }
            ],
            "data_quality_notes": [
                "internal inconsistencies in the source PDF, e.g. a log time that contradicts its photo timestamps"
            ],
            "urgent_items": [
                "items a board member must act on today; empty array if none"
            ],
            "html_summary": "HTML string summarizing the report (no html/body tags, just content markup)",
            "markdown_summary": "Markdown version of the same summary, described below"
        }

        The markdown_summary is a standalone file a board member reads on its own, so it must not
        assume the reader has the PDF or the dashboard open. Structure it as:

        # <Property> Security Report - <human readable date>
        ## Urgent Items          (say "None." when there are none; never omit the section)
        ## Report Summary        (period covered, officer, counts of incidents/patrols/checks)
        ## Historical Context    (what this shift means against the history above; this is the
                                 section that earns the file, so put the repeat/pattern/anomaly
                                 findings here, with counts and dates)
        ## Routine Activities Completed
        ## Incidents             (say "No incidents reported." when there are none)
        ## Parking Violations
                                 (list each plate with its history: "first time recorded", or
                                 "4th violation, first seen 2026-05-04". Unreadable tags are
                                 listed as unidentified with no history claim.)
        ## Maintenance Issues
        ## Safety Concerns
        ## Additional Notes      (data quality findings go here)

        Keep html_summary and markdown_summary consistent with each other and with the structured
        data; do not state a count in prose that contradicts the arrays above.

        Return ONLY valid JSON. No markdown fences or explanation.
        """;
    }
}

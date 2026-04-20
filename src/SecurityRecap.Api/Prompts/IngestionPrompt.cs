namespace SecurityRecap.Api.Prompts;

public static class IngestionPrompt
{
    public static string Build(string incidentHistoryJson)
    {
        return $$"""
        You are a security report analyst for an HOA property. You will receive a PDF of a daily
        security patrol report. Analyze it thoroughly and extract all structured data.

        Here is the recent incident history for context (last 30 days):
        {{incidentHistoryJson}}

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
                    "plate_number": "string",
                    "plate_state": "string",
                    "make": "string",
                    "model": "string",
                    "color": "string",
                    "violation_type": "string",
                    "location": "string"
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
                    "pattern": "string description of the pattern",
                    "related_incidents": ["list of incident descriptions"],
                    "recommendation": "string"
                }
            ],
            "html_summary": "HTML string summarizing the report (no html/body tags, just content markup)"
        }

        Return ONLY valid JSON. No markdown fences or explanation.
        """;
    }
}

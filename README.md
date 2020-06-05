# TDC.Tools.ProjectTimer

ProjectTimer is a smal and simple windows application that queries the eventlog for the eventlog start (6009) and the eventlog stop (6005) entries.

For each day the first start entry and the last stop entry ist stored in an xml file. The timestamps are converted to industry standard consultant time stamps (15 Minute steps). The result can be exported to Microsoft Excel.

The tool is useful if you want to track your working times on a project if your working times match the computer running times. The resulting Excel file can be used for your contract billing.

If no start or end timestamp is detected for a day you can manually enter the times. You can also add a break. The break times will follow the standard german working times law (working times > 6 hours a day - at least half an hour break, working times > 9 hours a day - at least 45 minutes break).

## Installation

No installation required - just clone the repository and build.

## Used Libraries

- Caliburn.Micro
- EPPlus
- Costura.Fody
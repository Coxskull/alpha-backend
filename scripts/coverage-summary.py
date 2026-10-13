"""Reject empty coverage and report both assembly-wide and non-migration source coverage."""
import json, pathlib, sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
valid = int(root.get('lines-valid', '0'))
covered = int(root.get('lines-covered', '0'))
if valid <= 0 or covered <= 0:
    raise SystemExit('Coverage evidence is empty; check source path mapping and instrumentation.')
source_lines = {}
for cls in root.findall('.//class'):
    filename = cls.get('filename', '').replace('\\', '/')
    if '/Migrations/' in '/' + filename or filename.startswith('Migrations/'):
        continue
    for line in cls.findall('./lines/line'):
        key = (filename, line.get('number'))
        source_lines[key] = max(source_lines.get(key, 0), int(line.get('hits', '0')))
non_migration_covered = sum(hits > 0 for hits in source_lines.values())
summary = {
    'assembly': {'linesCovered': covered, 'linesValid': valid, 'lineRate': float(root.get('line-rate', '0')),
                 'branchesCovered': int(root.get('branches-covered', '0')), 'branchesValid': int(root.get('branches-valid', '0')),
                 'branchRate': float(root.get('branch-rate', '0')), 'includesGeneratedMigrations': True},
    'nonMigrationSource': {'uniqueLinesCovered': non_migration_covered, 'uniqueLinesValid': len(source_lines),
                           'lineRate': non_migration_covered / len(source_lines) if source_lines else 0},
    'scope': 'Merged unit and disposable PostgreSQL/HTTP integration coverage; migration code shown separately'
}
pathlib.Path(sys.argv[2]).write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))

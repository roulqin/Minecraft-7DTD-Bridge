"""Validate evidence from the actual isolated 7DTD process."""
from pathlib import Path
import sys

root = Path(__file__).resolve().parent.parent
evidence = root / 'work/phase3851-test/evidence'
results = (evidence / 'prototype-results.txt').read_text(encoding='utf-8-sig')
log = (evidence / '7dtd-game.log').read_text(encoding='utf-8-sig')
checks = {
    'Model Load': 'MODEL_LOAD PASS' in results,
    'PNG Texture': 'PNG_TEXTURE PASS' in results,
    'Humanoid Avatar': 'HUMANOID_AVATAR PASS' in results,
    'Idle Animator': 'IDLE_ANIMATOR PASS' in results,
    'Walk Animator': 'WALK_ANIMATOR PASS' in results,
    'Independent World Preview': 'WORLD_PREVIEW placed_near_local_player' in results,
    'Bridge Isolation': "Initialized code in mod 'MC7DTD-Bridge'" not in log,
    'No Prototype Error': 'PROTOTYPE ERROR' not in results,
    'Render Captures': all((evidence / f'alex-slim-{pose}.png').is_file() for pose in ['tpose','idle','walk']),
    'Cleanup Requested': 'CLEANUP scheduled' in results,
}
report = '\n'.join(f'{name} {"PASS" if ok else "FAIL"}' for name,ok in checks.items())
(evidence / 'verification.txt').write_text(report+'\n', encoding='utf-8')
print(report)
sys.exit(0 if all(checks.values()) else 1)

# Checks the mod's localization (Localization/<culture>/Mods.ModernRecipeBook.<Area>.hjson):
# - every key the sources name exists in English, and English has no key the sources never name;
# - Russian, which the mod ships complete, has every English key;
# - no culture has a key English lacks, and every translated value keeps the English placeholders ({0}, {^0:...}).
# A key is named as a string literal "Area.Key" whose Area is one of the localization files, as
# "Mods.ModernRecipeBook.Area.Key", or as an item category's label (Add("key", "Label", icon, ...)).
# usage: python check_localization.py <mod folder>
import re
import sys
from pathlib import Path

import hjson

mod = Path(sys.argv[1])
prefix = 'Mods.ModernRecipeBook.'


def flat(d, path=''):
    for k, v in d.items():
        if isinstance(v, dict):
            yield from flat(v, path + k + '.')
        else:
            yield path + k, v


def load(culture, book_only=True):
    out = {}
    for f in (mod / 'Localization' / culture).glob(prefix + '*.hjson'):
        area = f.stem[len(prefix):]
        if book_only and area in ('Configs', 'Keybinds'):
            continue
        out.update(flat(hjson.loads(f.read_text(encoding='utf-8')), area + '.'))
    return out


def placeholders(text):
    return sorted(set(re.findall(r'\{\^?(\d+)', text)))


en = load('en-US')
areas = {k.split('.')[0] for k in en}
used = set()
for f in (mod / 'src').rglob('*.cs'):
    body = f.read_text(encoding='utf-8')
    used |= {k for k in re.findall(r'"([A-Z][A-Za-z]*\.[A-Za-z0-9]+)"', body) if k.split('.')[0] in areas}
    used |= {k[len(prefix):] for k in re.findall(r'"(Mods\.ModernRecipeBook\.[A-Za-z0-9.]+)"', body)}
    used |= {'Categories.' + k for k in re.findall(r'Add\("\w+", "(\w+)", \d+', body)}

problems = [f'en-US lacks {k}' for k in sorted(used - en.keys())]
problems += [f'en-US has unused {k}' for k in sorted(en.keys() - used)]
english = load('en-US', book_only=False)
for folder in sorted((mod / 'Localization').iterdir()):
    if not folder.is_dir() or folder.name == 'en-US':
        continue
    texts = load(folder.name, book_only=False)
    if folder.name == 'ru-RU':
        problems += [f'ru-RU lacks {k}' for k in sorted(english.keys() - texts.keys())]
    for k, v in texts.items():
        if k not in english:
            problems.append(f'{folder.name} has {k}, which en-US lacks')
        elif placeholders(v) != placeholders(english[k]):
            problems.append(f'{folder.name} {k}: placeholders {placeholders(v)} differ from en-US {placeholders(english[k])}')

print(f'{len(used)} book keys used, {len(en)} in en-US; {len(problems)} problems')
for p in problems:
    print('  ' + p)
sys.exit(1 if problems else 0)

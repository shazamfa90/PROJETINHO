#!/usr/bin/env bash
# Gera o Necess.exe (e o Necess.zip) a partir de src/ e assets/.
# Precisa do compilador C# do Mono:  apt install mono-mcs libmono-system-windows-forms4.0-cil
# O .exe resultante roda em qualquer Windows 7/10/11 com .NET Framework 4.x (ja vem no Windows).
set -euo pipefail
cd "$(dirname "$0")"

mcs -nologo -target:winexe -out:Necess.exe -optimize+ -debug- -codepage:utf8 \
    -win32icon:assets/Necess.ico \
    -resource:assets/Necess.ico,Necess.ico \
    -resource:assets/Necess.png,Necess.png \
    -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    src/AssemblyInfo.cs src/Program.cs src/Core.cs src/Theme.cs src/FlatButton.cs \
    src/CodeBackdrop.cs src/MainForm.cs

python3 - <<'PY'
import zipfile, os
if os.path.exists('Necess.zip'): os.remove('Necess.zip')
with zipfile.ZipFile('Necess.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    z.write('Necess.exe', 'Necess.exe')   # so o .exe: ele ja leva icone e logo dentro
print('Necess.exe', os.path.getsize('Necess.exe'), 'bytes |', 'Necess.zip', os.path.getsize('Necess.zip'), 'bytes')
PY

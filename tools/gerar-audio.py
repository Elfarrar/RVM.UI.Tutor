"""Gera a narração dos passos de um tour com o Kokoro TTS (offline). Ferramenta de quem escreve o
roteiro, nunca dependência do pacote: o Tutor só toca o mp3 que sai daqui.

Uso (com o venv do Kokoro; instalação em docs/uso.md § Apresentador):
    ~/.local/share/kokoro/venv/bin/python -I tools/gerar-audio.py <roteiro.json> <pasta-de-saida>

roteiro.json:
    {"voz": "pf_dora", "idioma": "pt-br", "velocidade": 1.0,
     "falas": {"passo-1": "Texto falado no passo 1.", "passo-2": "..."}}

Cada fala vira <pasta-de-saida>/<nome>.mp3. Idempotente: fala cujo texto e voz não mudaram não é
gerada de novo (o .json ao lado do mp3 guarda o que gerou).
"""
import json
import os
import sys
from pathlib import Path

import soundfile as sf
from kokoro_onnx import Kokoro

MODELO = Path(os.environ.get("KOKORO_MODEL", Path.home() / ".local/share/kokoro/model"))


def main(roteiro: Path, saida: Path) -> None:
    cfg = json.loads(roteiro.read_text(encoding="utf-8"))
    voz, idioma, velocidade = cfg["voz"], cfg["idioma"], cfg.get("velocidade", 1.0)
    saida.mkdir(parents=True, exist_ok=True)
    kokoro = None
    for nome, texto in cfg["falas"].items():
        mp3, marca = saida / f"{nome}.mp3", saida / f"{nome}.json"
        feito = {"voz": voz, "idioma": idioma, "velocidade": velocidade, "texto": texto}
        if mp3.exists() and marca.exists() and json.loads(marca.read_text(encoding="utf-8")) == feito:
            print(f"igual   {mp3}")
            continue
        kokoro = kokoro or Kokoro(str(MODELO / "kokoro-v1.0.onnx"), str(MODELO / "voices-v1.0.bin"))
        audio, taxa = kokoro.create(texto, voice=voz, speed=velocidade, lang=idioma)
        sf.write(mp3, audio, taxa, format="MP3")
        marca.write_text(json.dumps(feito, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"gerado  {mp3} ({len(audio) / taxa:.1f}s)")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]))

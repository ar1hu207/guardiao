"""Leitura do `zonas_risco.csv` gerado pelo ML, convertendo cada linha em entidades do domínio."""
from __future__ import annotations

import csv
import io

from app.domain.entities import Coordenada, ScoreFaixa, ZonaDeRisco
from app.domain.enums import FaixaHoraria, NivelRisco
from app.domain.exceptions import DominioInvalido

COLUNAS_OBRIGATORIAS = {"zona_id", "nome", "latitude", "longitude", "faixa_horaria", "score", "nivel"}


def _opcional(valor: str | None, tipo):
    if valor is None or str(valor).strip() == "":
        return None
    return tipo(valor)


def ler_zonas(conteudo: bytes) -> tuple[list[ZonaDeRisco], int]:
    """Devolve (zonas, total_de_linhas). Qualquer linha inválida aborta a importação inteira."""
    try:
        texto = conteudo.decode("utf-8-sig")
    except UnicodeDecodeError as e:
        raise DominioInvalido("O arquivo precisa estar em UTF-8.") from e

    leitor = csv.DictReader(io.StringIO(texto))
    faltando = COLUNAS_OBRIGATORIAS - set(leitor.fieldnames or [])
    if faltando:
        raise DominioInvalido(f"Colunas obrigatórias ausentes no CSV: {sorted(faltando)}")

    zonas: dict[int, ZonaDeRisco] = {}
    linhas = 0
    for n, linha in enumerate(leitor, start=2):  # linha 1 é o cabeçalho
        linhas += 1
        try:
            zona_id = int(linha["zona_id"])
            score = ScoreFaixa(
                faixa=FaixaHoraria(linha["faixa_horaria"].strip().upper()),
                score=float(linha["score"]),
                risco_relativo=_opcional(linha.get("risco_relativo"), float),
                taxa_grave_observada=_opcional(linha.get("taxa_grave_observada"), float),
                n_sinistros=_opcional(linha.get("n_sinistros"), int),
            )
            nivel_csv = NivelRisco(linha["nivel"].strip().upper())
            if nivel_csv != score.nivel:
                raise DominioInvalido(
                    f"nível {nivel_csv.value} não bate com o score {score.score} (esperado {score.nivel.value})")
            zona = zonas.get(zona_id)
            if zona is None:
                zona = ZonaDeRisco(id=zona_id, nome=linha["nome"],
                                   centro=Coordenada(float(linha["latitude"]), float(linha["longitude"])))
                zonas[zona_id] = zona
            zona.adicionar_score(score)
        except (ValueError, KeyError, DominioInvalido) as e:
            raise DominioInvalido(f"Linha {n} do CSV inválida: {e}") from e

    if not zonas:
        raise DominioInvalido("O CSV não tem nenhuma zona.")
    return list(zonas.values()), linhas

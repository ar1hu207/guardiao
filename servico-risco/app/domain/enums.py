"""Enumerações do domínio de risco."""
from __future__ import annotations

from enum import Enum

from app.domain.exceptions import DominioInvalido


class FaixaHoraria(str, Enum):
    """Faixas do dia usadas pelo Infosiga e pelo modelo de ML."""

    MADRUGADA = "MADRUGADA"
    MANHA = "MANHA"
    TARDE = "TARDE"
    NOITE = "NOITE"

    @property
    def hora_inicio(self) -> int:
        return _LIMITES[self][0]

    @property
    def hora_fim(self) -> int:
        return _LIMITES[self][1]

    @classmethod
    def de_hora(cls, hora: int) -> "FaixaHoraria":
        """Converte uma hora (0 a 23) na faixa horária correspondente."""
        if not isinstance(hora, int) or isinstance(hora, bool) or not 0 <= hora <= 23:
            raise DominioInvalido(f"Hora inválida: {hora!r}. Use um inteiro de 0 a 23.")
        for faixa, (inicio, fim) in _LIMITES.items():
            if inicio <= hora <= fim:
                return faixa
        raise AssertionError("faixas cobrem 0..23")  # pragma: no cover


_LIMITES: dict[FaixaHoraria, tuple[int, int]] = {
    FaixaHoraria.MADRUGADA: (0, 5),
    FaixaHoraria.MANHA: (6, 11),
    FaixaHoraria.TARDE: (12, 17),
    FaixaHoraria.NOITE: (18, 23),
}


class NivelRisco(str, Enum):
    """Nível exibido no app. É sempre derivado do score (regra do domínio)."""

    BAIXO = "BAIXO"
    MEDIO = "MEDIO"
    ALTO = "ALTO"

    @classmethod
    def de_score(cls, score: float) -> "NivelRisco":
        if score >= LIMITE_ALTO:
            return cls.ALTO
        if score >= LIMITE_MEDIO:
            return cls.MEDIO
        return cls.BAIXO


# Mesmos cortes usados no notebook de ML (ml/guardiao_risco_ml.ipynb, seção 10).
LIMITE_MEDIO = 0.40
LIMITE_ALTO = 0.80

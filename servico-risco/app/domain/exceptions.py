"""Erros de negócio. A camada de API traduz cada um para um código HTTP."""


class ErroDominio(Exception):
    """Base de todos os erros de negócio."""


class DominioInvalido(ErroDominio):
    """Um dado viola uma regra do domínio (vira HTTP 400)."""


class ZonaNaoEncontrada(ErroDominio):
    """A zona pedida não existe (vira HTTP 404)."""

    def __init__(self, zona_id: int):
        super().__init__(f"Zona de risco {zona_id} não encontrada.")
        self.zona_id = zona_id


class ImportacaoDuplicada(ErroDominio):
    """O mesmo arquivo de zonas já é a importação ativa (vira HTTP 409)."""

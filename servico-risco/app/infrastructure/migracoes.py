"""Aplica as migrações Alembic por código (usado na inicialização e nos testes)."""
from __future__ import annotations

from alembic import command
from alembic.config import Config

from app.config import RAIZ_SERVICO


def config_alembic(database_url: str) -> Config:
    cfg = Config(str(RAIZ_SERVICO / "alembic.ini"))
    cfg.set_main_option("script_location", str(RAIZ_SERVICO / "migrations"))
    cfg.set_main_option("sqlalchemy.url", database_url)
    cfg.attributes["configure_logger"] = False
    return cfg


def aplicar_migracoes(database_url: str, revisao: str = "head") -> None:
    command.upgrade(config_alembic(database_url), revisao)


def reverter_migracoes(database_url: str, revisao: str = "base") -> None:
    command.downgrade(config_alembic(database_url), revisao)

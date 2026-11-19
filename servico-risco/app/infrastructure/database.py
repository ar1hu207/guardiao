"""Conexão com o banco (SQLAlchemy 2.0)."""
from __future__ import annotations

from sqlalchemy import Engine, create_engine, event
from sqlalchemy.orm import DeclarativeBase, sessionmaker


class Base(DeclarativeBase):
    pass


def criar_engine(database_url: str) -> Engine:
    eh_sqlite = database_url.startswith("sqlite")
    engine = create_engine(
        database_url,
        connect_args={"check_same_thread": False} if eh_sqlite else {},
        future=True,
    )
    if eh_sqlite:
        # SQLite só respeita chave estrangeira com esta pragma ligada em cada conexão.
        @event.listens_for(engine, "connect")
        def _ligar_fk(dbapi_conn, _):  # pragma: no cover - executado pelo driver
            cursor = dbapi_conn.cursor()
            cursor.execute("PRAGMA foreign_keys=ON")
            cursor.close()
    return engine


def criar_fabrica_sessao(engine: Engine) -> sessionmaker:
    return sessionmaker(bind=engine, autoflush=False, expire_on_commit=False)

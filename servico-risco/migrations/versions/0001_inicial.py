"""Migração inicial: zonas de risco e score por faixa horária.

Revision ID: 0001_inicial
Revises:
Create Date: 2026-11-12
"""
import sqlalchemy as sa
from alembic import op

revision = "0001_inicial"
down_revision = None
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "zonas",
        sa.Column("id", sa.Integer(), primary_key=True, autoincrement=False),
        sa.Column("nome", sa.String(200), nullable=False),
        sa.Column("latitude", sa.Float(), nullable=False),
        sa.Column("longitude", sa.Float(), nullable=False),
    )
    op.create_index("ix_zonas_latitude", "zonas", ["latitude"])
    op.create_index("ix_zonas_longitude", "zonas", ["longitude"])

    op.create_table(
        "scores_zona",
        sa.Column("id", sa.Integer(), primary_key=True, autoincrement=True),
        sa.Column("zona_id", sa.Integer(), sa.ForeignKey("zonas.id", ondelete="CASCADE"), nullable=False),
        sa.Column("faixa", sa.String(12), nullable=False),
        sa.Column("score", sa.Float(), nullable=False),
        sa.Column("nivel", sa.String(5), nullable=False),
        sa.Column("risco_relativo", sa.Float()),
        sa.Column("taxa_grave_observada", sa.Float()),
        sa.Column("n_sinistros", sa.Integer()),
        sa.UniqueConstraint("zona_id", "faixa", name="uq_score_zona_faixa"),
        sa.CheckConstraint("score >= 0 AND score <= 1", name="ck_score_intervalo"),
    )
    op.create_index("ix_scores_zona_zona_id", "scores_zona", ["zona_id"])


def downgrade() -> None:
    op.drop_index("ix_scores_zona_zona_id", table_name="scores_zona")
    op.drop_table("scores_zona")
    op.drop_index("ix_zonas_longitude", table_name="zonas")
    op.drop_index("ix_zonas_latitude", table_name="zonas")
    op.drop_table("zonas")

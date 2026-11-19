"""Evolução do esquema: versão do modelo de ML em cada score e auditoria das consultas.

- Nova tabela `importacoes_modelo`: cada carga do zonas_risco.csv (arquivo, hash, totais, data).
- Nova coluna `scores_zona.importacao_id` (FK): de qual versão do modelo veio cada score.
- Nova tabela `consultas_risco`: cada consulta do app (ponto, hora, zona encontrada).

Revision ID: 0002_importacoes_e_consultas
Revises: 0001_inicial
Create Date: 2026-11-12
"""
import sqlalchemy as sa
from alembic import op

revision = "0002_importacoes_e_consultas"
down_revision = "0001_inicial"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "importacoes_modelo",
        sa.Column("id", sa.Integer(), primary_key=True, autoincrement=True),
        sa.Column("arquivo", sa.String(300), nullable=False),
        sa.Column("hash_sha256", sa.String(64), nullable=False),
        sa.Column("total_zonas", sa.Integer(), nullable=False),
        sa.Column("total_linhas", sa.Integer(), nullable=False),
        sa.Column("criada_em", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index("ix_importacoes_modelo_hash_sha256", "importacoes_modelo", ["hash_sha256"])

    with op.batch_alter_table("scores_zona") as batch:
        batch.add_column(sa.Column("importacao_id", sa.Integer(), nullable=True))
        batch.create_foreign_key("fk_scores_zona_importacao", "importacoes_modelo", ["importacao_id"], ["id"])
        batch.create_index("ix_scores_zona_importacao_id", ["importacao_id"])

    op.create_table(
        "consultas_risco",
        sa.Column("id", sa.Integer(), primary_key=True, autoincrement=True),
        sa.Column("latitude", sa.Float(), nullable=False),
        sa.Column("longitude", sa.Float(), nullable=False),
        sa.Column("hora", sa.Integer(), nullable=False),
        sa.Column("faixa", sa.String(12), nullable=False),
        sa.Column("zona_id", sa.Integer(), sa.ForeignKey("zonas.id", ondelete="SET NULL"), nullable=True),
        sa.Column("criada_em", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index("ix_consultas_risco_zona_id", "consultas_risco", ["zona_id"])


def downgrade() -> None:
    op.drop_index("ix_consultas_risco_zona_id", table_name="consultas_risco")
    op.drop_table("consultas_risco")
    with op.batch_alter_table("scores_zona") as batch:
        batch.drop_index("ix_scores_zona_importacao_id")
        batch.drop_constraint("fk_scores_zona_importacao", type_="foreignkey")
        batch.drop_column("importacao_id")
    op.drop_index("ix_importacoes_modelo_hash_sha256", table_name="importacoes_modelo")
    op.drop_table("importacoes_modelo")

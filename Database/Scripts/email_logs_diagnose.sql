-- Liste colunas reais de public.email_logs (inclui nomes entre aspas / maiúsculas).
-- Útil se email_logs_align_model.sql não encontrar a coluna do destinatário.

-- Se existir email_logs em outro schema (não public), ajuste o script ou o EF:
-- SELECT table_schema, table_name FROM information_schema.tables WHERE table_name = 'email_logs';

SELECT
  a.attname AS coluna_em_pg_catalog,
  pg_catalog.format_type(a.atttypid, a.atttypmod) AS tipo
FROM pg_catalog.pg_attribute a
JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname = 'email_logs'
  AND a.attnum > 0
  AND NOT a.attisdropped
ORDER BY a.attnum;

-- Comparativo via information_schema (só identificadores normalizados em minúsculas):
-- SELECT column_name, data_type
-- FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'email_logs'
-- ORDER BY ordinal_position;

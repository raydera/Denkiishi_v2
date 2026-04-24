-- Denkiishi: ajustes opcionais em public.email_logs.
-- Esquema real (seu BD): id, user_id, email_recipient, subject, status, error_message,
--   sent_at, environment, created_at (+ body se existir).
-- O EF mapeia a propriedade ToEmail → coluna email_recipient (ver EmailLog.cs).
--
-- Idempotente. Só é necessário se ainda existir nome antigo (ex.: to_email) ou faltar coluna.

DO $$
DECLARE
  src name;
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name = 'email_logs'
  ) THEN
    RAISE NOTICE 'Tabela public.email_logs não existe.';
    RETURN;
  END IF;

  -- Migração de nomes antigos → email_recipient (destinatário)
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'email_recipient'
  ) THEN
    IF EXISTS (
      SELECT 1 FROM information_schema.columns
      WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'to_email'
    ) THEN
      ALTER TABLE public.email_logs RENAME COLUMN to_email TO email_recipient;
    ELSE
      SELECT a.attname INTO src
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public'
        AND c.relname = 'email_logs'
        AND a.attnum > 0
        AND NOT a.attisdropped
        AND lower(a.attname::text) IN (
          'toemail', 'email', 'recipient', 'recipient_email', 'mail_to',
          'destinatario', 'mail', 'endereco_email', 'address', 'to'
        )
      ORDER BY CASE lower(a.attname::text)
        WHEN 'toemail' THEN 1
        WHEN 'email' THEN 2
        WHEN 'recipient_email' THEN 3
        WHEN 'recipient' THEN 4
        WHEN 'mail_to' THEN 5
        WHEN 'mail' THEN 6
        WHEN 'address' THEN 7
        WHEN 'to' THEN 8
        WHEN 'destinatario' THEN 9
        WHEN 'endereco_email' THEN 10
      END
      LIMIT 1;

      IF src IS NOT NULL THEN
        EXECUTE format('ALTER TABLE public.email_logs RENAME COLUMN %I TO email_recipient', src);
      ELSE
        RAISE NOTICE 'Defina manualmente: ALTER TABLE public.email_logs RENAME COLUMN <coluna> TO email_recipient;';
      END IF;
    END IF;
  END IF;

  -- Corpo da mensagem (EmailLog.Body) — adiciona só se ainda não existir
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'body'
  ) THEN
    ALTER TABLE public.email_logs
      ADD COLUMN body text NOT NULL DEFAULT '';
  END IF;

  -- user_id
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'user_id'
  ) AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'userid'
  ) THEN
    ALTER TABLE public.email_logs RENAME COLUMN userid TO user_id;
  END IF;

  -- error_message
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'error_message'
  ) AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'errormessage'
  ) THEN
    ALTER TABLE public.email_logs RENAME COLUMN errormessage TO error_message;
  END IF;

  -- created_at
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'created_at'
  ) AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'createdat'
  ) THEN
    ALTER TABLE public.email_logs RENAME COLUMN createdat TO created_at;
  ELSIF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'email_logs' AND column_name = 'created_at'
  ) THEN
    ALTER TABLE public.email_logs
      ADD COLUMN created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP;
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_email_logs_created_at ON public.email_logs (created_at);

-- Opcional: default no BD para environment (o app já envia IHostEnvironment.EnvironmentName).
-- ALTER TABLE public.email_logs ALTER COLUMN environment SET DEFAULT 'Production';

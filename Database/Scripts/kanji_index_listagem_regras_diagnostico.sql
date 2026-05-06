-- =============================================================================
-- Diagnóstico: por que um kanji aparece (ou não) na tela /Kanji (KanjiController.Index)
-- Banco: PostgreSQL — alinhado a Controllers/KanjiController.cs → Index
--
-- COMO USAR
--   1) Edite o INSERT em _kanji_diag_params (logo abaixo).
--   2) Execute o script inteiro no seu cliente SQL (pgAdmin, DBeaver, psql, etc.).
--
-- PARÂMETROS DO INSERT
--   kanji_id            → ID na tabela kanji que você quer investigar.
--   category_id_filter  → Categoria do dropdown na tela; use 0 para simular o default
--                         da app (primeira cujo nome contém "JLPT", senão primeira por nome).
--   circle_id_filter    → ID do círculo na URL (?circleId=); use 0 se você não usa filtro de círculo.
--   switch_modal_kanji  → Mesmo que switchModalKanji na URL (modo estrito só nativos do círculo).
-- =============================================================================

DROP TABLE IF EXISTS _kanji_diag_params;
CREATE TEMP TABLE _kanji_diag_params (
    kanji_id             int     NOT NULL,
    category_id_filter   int     NOT NULL DEFAULT 0,
    circle_id_filter     int     NOT NULL DEFAULT 0,
    switch_modal_kanji     boolean NOT NULL DEFAULT false
);

-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- <<< EDITE APENAS ESTA LINHA >>>
INSERT INTO _kanji_diag_params (kanji_id, category_id_filter, circle_id_filter, switch_modal_kanji)
VALUES (0, 0, 0, false);
-- >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
-- Exemplo: VALUES (4521, 0, 12, false);
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) Dados básicos do kanji
-- -----------------------------------------------------------------------------
SELECT
    k.id,
    k.literal,
    k.is_active,
    CASE
        WHEN k.is_active IS DISTINCT FROM TRUE THEN 'FALHA: is_active deve ser TRUE para aparecer na listagem.'
        ELSE 'OK: is_active.'
    END AS check_is_active
FROM kanji k
CROSS JOIN _kanji_diag_params p
WHERE k.id = p.kanji_id;

-- -----------------------------------------------------------------------------
-- 2) Qual categoria o script está usando (equivalente ao default JLPT / primeira)
-- -----------------------------------------------------------------------------
WITH jlpt AS (
    SELECT id, name FROM category WHERE name ILIKE '%JLPT%' ORDER BY id LIMIT 1
),
first_cat AS (
    SELECT id, name FROM category ORDER BY name LIMIT 1
),
chosen AS (
    SELECT COALESCE(
        NULLIF(p.category_id_filter, 0),
        (SELECT id FROM jlpt),
        (SELECT id FROM first_cat)
    ) AS id
    FROM _kanji_diag_params p
)
SELECT
    c.id   AS category_id_usado,
    c.name AS category_name,
    CASE WHEN c.id IS NULL THEN 'FALHA: não há categoria no banco.' ELSE 'OK.' END AS check_category
FROM chosen
LEFT JOIN category c ON c.id = chosen.id;

-- -----------------------------------------------------------------------------
-- 2b) Vínculo obrigatório: kanji_category_map + categoria resolvida (JLPT / primeira)
-- -----------------------------------------------------------------------------
SELECT
    prm.kanji_id,
    cat.cat_id AS category_id_resolvido,
    kcm.category_level,
    CASE
        WHEN kcm.kanji_id IS NULL THEN
            'FALHA: não há linha em kanji_category_map para este kanji nesta categoria. A Index só lista por esta junção — só estar em "kanji" não basta.'
        ELSE 'OK: kanji ligado à categoria.'
    END AS check_kanji_category_map
FROM _kanji_diag_params prm
CROSS JOIN LATERAL (
    SELECT COALESCE(
        NULLIF(prm.category_id_filter, 0),
        (SELECT id FROM category WHERE name ILIKE '%JLPT%' ORDER BY id LIMIT 1),
        (SELECT id FROM category ORDER BY name LIMIT 1)
    ) AS cat_id
) cat
LEFT JOIN kanji_category_map kcm
    ON kcm.kanji_id = prm.kanji_id AND kcm.category_id = cat.cat_id;

-- -----------------------------------------------------------------------------
-- 3) Filtro de círculo (circleId na URL). Se circle_id_filter = 0, a app NÃO aplica isto.
-- -----------------------------------------------------------------------------
WITH prm AS (SELECT * FROM _kanji_diag_params),
circle_ctx AS (
    SELECT c.id AS circle_id, c.mandala_id, c.sequential AS circle_seq
    FROM circle c
    JOIN prm ON c.id = prm.circle_id_filter AND prm.circle_id_filter <> 0
),
known_radicals AS (
    SELECT DISTINCT cui.radical_id
    FROM circle_ue_item cui
    JOIN circle c ON c.id = cui.circle_id
    JOIN circle_ctx ctx ON c.mandala_id = ctx.mandala_id AND c.sequential <= ctx.circle_seq
    WHERE cui.radical_id IS NOT NULL
),
native_kanji AS (
    SELECT DISTINCT cui.kanji_id
    FROM circle_ue_item cui
    JOIN prm ON cui.circle_id = prm.circle_id_filter AND prm.circle_id_filter <> 0
    WHERE cui.kanji_id IS NOT NULL
),
vocab_kanji AS (
    SELECT DISTINCT vc.kanji_id
    FROM circle_ue_item cui
    JOIN vocabulary_composition vc ON vc.vocabulary_id = cui.vocabulary_id
    JOIN prm ON cui.circle_id = prm.circle_id_filter AND prm.circle_id_filter <> 0
    WHERE cui.vocabulary_id IS NOT NULL
),
suggested_kanji AS (
    SELECT k.id AS kanji_id
    FROM kanji k
    CROSS JOIN prm
    WHERE prm.circle_id_filter <> 0
      AND EXISTS (SELECT 1 FROM kanji_radical kr WHERE kr.kanji_id = k.id)
      AND NOT EXISTS (
          SELECT 1
          FROM kanji_radical kr
          WHERE kr.kanji_id = k.id
            AND kr.radical_id NOT IN (SELECT radical_id FROM known_radicals)
      )
),
combined AS (
    SELECT kanji_id FROM native_kanji
    UNION SELECT kanji_id FROM vocab_kanji
    UNION SELECT kanji_id FROM suggested_kanji
),
strict_only AS (
    SELECT kanji_id FROM native_kanji
)
SELECT
    prm.circle_id_filter AS circle_id_param,
    CASE
        WHEN prm.circle_id_filter = 0 THEN 'circle_id = 0: URL sem circleId — ignore esta secção.'
        ELSE 'Filtro de círculo ativo.'
    END AS nota,
    EXISTS (SELECT 1 FROM native_kanji nk WHERE nk.kanji_id = prm.kanji_id) AS passa_nativo_circulo,
    EXISTS (SELECT 1 FROM vocab_kanji vk WHERE vk.kanji_id = prm.kanji_id) AS passa_via_vocabulario_circulo,
    EXISTS (SELECT 1 FROM suggested_kanji sk WHERE sk.kanji_id = prm.kanji_id) AS passa_sugerido_pela_logica_radicais,
    CASE
        WHEN prm.circle_id_filter = 0 THEN NULL::boolean
        WHEN prm.switch_modal_kanji THEN EXISTS (SELECT 1 FROM strict_only s WHERE s.kanji_id = prm.kanji_id)
        ELSE EXISTS (SELECT 1 FROM combined c WHERE c.kanji_id = prm.kanji_id)
    END AS passa_filtro_circulo_modo_atual,
    CASE
        WHEN prm.circle_id_filter = 0 THEN NULL::text
        WHEN prm.switch_modal_kanji AND NOT EXISTS (SELECT 1 FROM strict_only s WHERE s.kanji_id = prm.kanji_id)
            THEN 'switchModalKanji=true: só kanjis com kanji_id em circle_ue_item deste círculo.'
        WHEN NOT prm.switch_modal_kanji AND NOT EXISTS (SELECT 1 FROM combined c WHERE c.kanji_id = prm.kanji_id)
            THEN 'Modo expandido: falhou nativo + vocabulário do círculo + sugestão por radicais.'
        ELSE 'OK para parte de círculo.'
    END AS motivo_observacao
FROM _kanji_diag_params prm;

-- -----------------------------------------------------------------------------
-- 4) Resultado final: entraria na query da Index com os mesmos parâmetros?
-- -----------------------------------------------------------------------------
WITH prm AS (SELECT * FROM _kanji_diag_params),
cat AS (
    SELECT COALESCE(
        NULLIF(prm.category_id_filter, 0),
        (SELECT id FROM category WHERE name ILIKE '%JLPT%' ORDER BY id LIMIT 1),
        (SELECT id FROM category ORDER BY name LIMIT 1)
    ) AS cat_id
    FROM _kanji_diag_params prm
),
base AS (
    SELECT k.id
    FROM kanji k
    JOIN kanji_category_map kcm ON kcm.kanji_id = k.id
    JOIN cat ON kcm.category_id = cat.cat_id
    JOIN _kanji_diag_params p ON k.id = p.kanji_id
    WHERE k.is_active IS TRUE
),
circle_ctx AS (
    SELECT c.id AS circle_id, c.mandala_id, c.sequential AS circle_seq
    FROM circle c
    JOIN _kanji_diag_params p ON p.circle_id_filter <> 0 AND c.id = p.circle_id_filter
),
known_radicals AS (
    SELECT DISTINCT cui.radical_id
    FROM circle_ue_item cui
    JOIN circle c ON c.id = cui.circle_id
    JOIN circle_ctx ctx ON c.mandala_id = ctx.mandala_id AND c.sequential <= ctx.circle_seq
    WHERE cui.radical_id IS NOT NULL
),
native_kanji AS (
    SELECT DISTINCT cui.kanji_id
    FROM circle_ue_item cui
    JOIN _kanji_diag_params p ON p.circle_id_filter <> 0 AND cui.circle_id = p.circle_id_filter
    WHERE cui.kanji_id IS NOT NULL
),
vocab_kanji AS (
    SELECT DISTINCT vc.kanji_id
    FROM circle_ue_item cui
    JOIN vocabulary_composition vc ON vc.vocabulary_id = cui.vocabulary_id
    JOIN _kanji_diag_params p ON p.circle_id_filter <> 0 AND cui.circle_id = p.circle_id_filter
    WHERE cui.vocabulary_id IS NOT NULL
),
suggested_kanji AS (
    SELECT k2.id AS kanji_id
    FROM kanji k2
    CROSS JOIN _kanji_diag_params p
    WHERE p.circle_id_filter <> 0
      AND EXISTS (SELECT 1 FROM kanji_radical kr WHERE kr.kanji_id = k2.id)
      AND NOT EXISTS (
          SELECT 1 FROM kanji_radical kr
          WHERE kr.kanji_id = k2.id
            AND kr.radical_id NOT IN (SELECT radical_id FROM known_radicals)
      )
),
combined AS (
    SELECT kanji_id FROM native_kanji
    UNION SELECT kanji_id FROM vocab_kanji
    UNION SELECT kanji_id FROM suggested_kanji
),
filtrado_circulo AS (
    SELECT b.id
    FROM base b
    CROSS JOIN _kanji_diag_params p
    WHERE p.circle_id_filter = 0
    UNION
    SELECT b.id
    FROM base b
    INNER JOIN combined c ON c.kanji_id = b.id
    CROSS JOIN _kanji_diag_params p
    WHERE p.circle_id_filter <> 0 AND NOT p.switch_modal_kanji
    UNION
    SELECT b.id
    FROM base b
    INNER JOIN native_kanji n ON n.kanji_id = b.id
    CROSS JOIN _kanji_diag_params p
    WHERE p.circle_id_filter <> 0 AND p.switch_modal_kanji
)
SELECT
    p.kanji_id,
    (SELECT id FROM base LIMIT 1) IS NOT NULL AS passa_base_categoria_e_ativo,
    p.circle_id_filter AS circle_id,
    p.switch_modal_kanji,
    EXISTS (SELECT 1 FROM filtrado_circulo f WHERE f.id = p.kanji_id) AS aparece_na_listagem_com_parametros,
    CASE
        WHEN (SELECT id FROM base LIMIT 1) IS NULL THEN
            'Corrija: kanji_category_map para a categoria do filtro e is_active = true.'
        WHEN p.circle_id_filter <> 0 AND NOT EXISTS (SELECT 1 FROM filtrado_circulo x WHERE x.id = p.kanji_id) THEN
            'Corrija filtro de círculo: circle_ue_item / vocabulary_composition / radicais.'
        ELSE
            'Com estes parâmetros o kanji entra na consulta da Index.'
    END AS proximo_passo
FROM _kanji_diag_params p;

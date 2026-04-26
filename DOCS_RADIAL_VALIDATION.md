# DOCS_RADIAL_VALIDATION

Este documento descreve o motor de **busca/filtragem radial** da tela **Radial de Kanjis** (`Views/Kanji/Index.cshtml`) e como ele foi unificado conceitualmente com o validador do Arquiteto (`ArchitectController.ValidateMatrix`).

> Referência de manutenção: procure por alterações marcadas com `// ARCH: RadialValidation`.

---

## Objetivo

Quando um **Círculo** é selecionado no filtro “Arquiteto” da tela de Kanjis, o sistema:

- Determina quais **radicais** o aluno já conhece (radicais mapeados em círculos anteriores da mesma Mandala, incluindo o círculo atual).
- **Sugere** Kanjis que podem ser estudados agora, pois **todos** os seus radicais componentes (tabela `kanji_radical`) já pertencem ao conjunto conhecido.
- Diferencia visualmente:
  - **Nativos do círculo**: Kanjis explicitamente mapeados no `circle_ue_item` do círculo atual.
  - **Sugeridos pela lógica**: Kanjis que passam na validação de subconjunto, mas **não** estão mapeados no círculo atual.

---

## Entidades e relacionamento (Arquiteto)

O Arquiteto organiza conteúdo em:

- `mandala` → `circle` → `circle_ue_item`

Tabelas relevantes (principais colunas):

### `mandala`
- `id` (PK)
- `sequential` (ordenação global da mandala)
- `text` (nome)

### `circle`
- `id` (PK)
- `mandala_id` (FK → `mandala.id`)
- `sequential` (ordenação **dentro** da mandala)
- `text` (nome)

### `circle_ue_item`
Itens mapeados no círculo (kanji / radical / vocabulary):

- `id` (PK)
- `circle_id` (FK → `circle.id`)
- `kanji_id` (nullable)
- `radical_id` (nullable)
- `vocabulary_id` (nullable)
- `sequential` (ordem do item no círculo)

---

## Decomposição de Kanjis em radicais

### `kanji_radical`
Mapeia a decomposição de um Kanji em radicais:

- `kanji_id` (PK composto)
- `radical_id` (PK composto)
- demais colunas (ex.: `role`, `position`, etc.)

O algoritmo de sugestão usa apenas (`kanji_id`, `radical_id`).

---

## Algoritmo (subconjunto / divisão relacional)

### 1) Identificar círculo atual
Para o `circleId` selecionado, buscamos:

- `MandalaId` do círculo
- `Sequential` do círculo (ordem do círculo dentro da mandala)

### 2) Construir o conjunto de radicais conhecidos
Definimos:

\[
KnownRadicals = \{ radical\_id \mid circle\_ue\_item.radical\_id \neq null \ \wedge \
circle.mandala\_id = MandalaAtual \ \wedge \ circle.sequential \le SequentialAtual \}
\]

Em outras palavras: “todo radical já introduzido até o círculo atual”.

### 3) Selecionar Kanjis válidos pela lógica (subconjunto)
Um Kanji \(K\) é **sugerido pela lógica** quando:

\[
Radicals(K) \subseteq KnownRadicals
\]

Na prática, isso é a clássica **divisão relacional** (todos os requisitos do item estão satisfeitos).

Implementação eficiente (forma equivalente):

- Para cada `kanji_id`, contamos quantos radicais ele tem no total.
- Contamos quantos desses radicais estão em `KnownRadicals`.
- O Kanji passa se as contagens forem iguais.

---

## Flags e UX

O DTO usado na grid (`KanjiStatusDto`) expõe:

- `IsNativeToCircle`: `true` quando o Kanji está mapeado em `circle_ue_item` do círculo atual.
- `IsSuggestedByLogic`: `true` quando passa na regra de subconjunto **e** não é nativo.

Na tela:

- Kanjis sugeridos recebem **borda pontilhada verde** e badge “Sugerido”.
- Ao clicar em um sugerido, o modal exibe:
  - “Este caractere foi sugerido com base nos radicais que você já conhece”.

---

## Alinhamento com `ValidateMatrix`

O endpoint `ArchitectController.ValidateMatrix` valida consistência de pré-requisitos (ex.: Kanji antes de radical, etc.).

Este motor da tela Radial segue o mesmo princípio: **um item é elegível quando seus componentes já foram introduzidos antes** (no contexto de Mandala + `circle.sequential`), porém aplicado especificamente a:

- Kanji → Radicais (`kanji_radical`)

---

## Performance e índices recomendados

### Padrão atual observado
No snapshot do modelo EF:

- `kanji_radical` tem PK composta (`kanji_id`, `radical_id`)
- existe índice em `radical_id`

### Recomendações
Para grandes volumes de dados, as consultas podem se beneficiar de:

- **Garantir índice por `kanji_id`**: já atendido pela PK composta iniciando por `kanji_id`.
- **Índice composto adicional** (opcional, se houver gargalo real):
  - `CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_kanji_radical_radical_kanji ON kanji_radical(radical_id, kanji_id);`

Motivo: o motor faz filtros por `radical_id` (lista `IN`) e precisa agrupar/contar por `kanji_id`.

> Importante: só crie índice após observar planos/estatísticas no Postgres (EXPLAIN ANALYZE), pois índices extras também custam INSERT/UPDATE e espaço.

---

## Pontos de manutenção

- Alterações críticas devem manter a tag: `// ARCH: RadialValidation`
- Qualquer mudança nas regras do Arquiteto (ordenação `sequential` por Mandala) deve ser refletida no cálculo de `KnownRadicals`.
- Se o sistema passar a considerar “radicais conhecidos” por usuário (progresso), o conjunto `KnownRadicals` deve deixar de ser apenas “mapeamento do Arquiteto” e passar a considerar progresso individual.


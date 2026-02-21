using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Denkiishi_v2.Models;

namespace Denkiishi_v2.Models;

public partial class InasDbContext : IdentityDbContext<ApplicationUser>
{
    public InasDbContext()
    {
    }

    public InasDbContext(DbContextOptions<InasDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Card> Cards { get; set; }
    public virtual DbSet<Deck> Decks { get; set; }
    public virtual DbSet<Kanji> Kanjis { get; set; }
    public virtual DbSet<KanjiDecomposition> KanjiDecompositions { get; set; }
    public virtual DbSet<KanjiMeaning> KanjiMeanings { get; set; }
    public virtual DbSet<KanjiRadical> KanjiRadicals { get; set; }
    public virtual DbSet<KanjiReading> KanjiReadings { get; set; }
    public virtual DbSet<KanjiSimilarity> KanjiSimilarities { get; set; }
    public virtual DbSet<Language> Language { get; set; }
    public virtual DbSet<Lesson> Lessons { get; set; }
    public virtual DbSet<Level> Levels { get; set; }
    public virtual DbSet<LevelItem> LevelItems { get; set; }
    public virtual DbSet<LevelItemType> LevelItemTypes { get; set; }
    public virtual DbSet<Radical> Radicals { get; set; }
    public virtual DbSet<RadicalMeaning> RadicalMeanings { get; set; }
    public virtual DbSet<ReviewHistory> ReviewHistories { get; set; }
    public virtual DbSet<User> GameUsers { get; set; }
    public virtual DbSet<UserNote> UserNotes { get; set; }
    public virtual DbSet<UserProgress> UserProgresses { get; set; }
    public virtual DbSet<UserSynonym> UserSynonyms { get; set; }
    public virtual DbSet<Vocabulary> Vocabularies { get; set; }
    public virtual DbSet<VocabularyContextSentence> VocabularyContextSentences { get; set; }
    public virtual DbSet<VocabularyMeaning> VocabularyMeanings { get; set; }
    public virtual DbSet<VocabularyReading> VocabularyReadings { get; set; }
    public virtual DbSet<VocabularyComposition> VocabularyCompositions { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<KanjiCategoryMap> KanjiCategoryMaps { get; set; }
    public virtual DbSet<KanjiAuditLog> KanjiAuditLogs { get; set; }
    public DbSet<VocabularyPartOfSpeech> VocabularyPartsOfSpeech { get; set; }
    public DbSet<VocabularyPartOfSpeechMap> VocabularyPartsOfSpeechMaps { get; set; }
    public virtual DbSet<SyntaxHighlight> SyntaxHighlights { get; set; }
    public virtual DbSet<RadicalMeaningMnemonic> RadicalMeaningMnemonics { get; set; }

    public virtual DbSet<KanjiMeaningMnemonic> KanjiMeaningMnemonics { get; set; }
    public virtual DbSet<KanjiReadingMnemonic> KanjiReadingMnemonics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VocabularyPartOfSpeechMap>(entity =>
        {
            // Define a chave primária composta
            entity.HasKey(e => new { e.VocabularyId, e.VocabularyPartOfSpeechId });

            // Relacionamento com Vocabulary
            entity.HasOne(e => e.Vocabulary)
                  .WithMany() // Se quiser acessar do Vocabulario, precisaremos adicionar uma Collection lá
                  .HasForeignKey(e => e.VocabularyId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relacionamento com PartOfSpeech
            entity.HasOne(e => e.PartOfSpeech)
                  .WithMany(p => p.VocabularyMaps)
                  .HasForeignKey(e => e.VocabularyPartOfSpeechId)
                  .OnDelete(DeleteBehavior.Restrict);
        });



        modelBuilder.Entity<KanjiCategoryMap>(entity =>
        {
            entity.HasKey(e => new { e.KanjiId, e.CategoryId });
            entity.ToTable("kanji_category_map");
            entity.HasOne(d => d.Kanji).WithMany(p => p.KanjiCategoryMaps).HasForeignKey(d => d.KanjiId);
            entity.HasOne(d => d.Category).WithMany(p => p.KanjiCategoryMaps).HasForeignKey(d => d.CategoryId);
        });

        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cartoes_pkey");
            entity.ToTable("card");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('cartoes_id_cartao_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.Back).HasColumnName("back");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("created_at");
            entity.Property(e => e.DeckId).HasColumnName("deck_id");
            entity.Property(e => e.Front).HasColumnName("front");
            entity.HasOne(d => d.Deck).WithMany(p => p.Cards).HasForeignKey(d => d.DeckId).HasConstraintName("fk_baralho_cartao");
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("baralhos_pkey");
            entity.ToTable("deck");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('baralhos_id_baralho_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.User).WithMany(p => p.Decks).HasForeignKey(d => d.UserId).HasConstraintName("fk_usuario_baralho");
        });

        modelBuilder.Entity<Kanji>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kanji_pkey");
            entity.ToTable("kanji");
            entity.HasIndex(e => e.WanikaniId, "idx_kanji_wanikani_id");
            entity.HasIndex(e => e.UnicodeCode, "kanji_unicode_code_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FrequencyRank).HasColumnName("frequency_rank");
            entity.Property(e => e.GradeLevel).HasColumnName("grade_level");
            entity.Property(e => e.IsRadical).HasDefaultValue(false).HasColumnName("is_radical");
            entity.Property(e => e.IsActive).HasDefaultValue(false).HasColumnName("is_active");
            entity.Property(e => e.JlptLevel).HasColumnName("jlpt_level");
            entity.Property(e => e.Literal).HasColumnName("literal");
            entity.Property(e => e.StrokeCount).HasColumnName("stroke_count");
            entity.Property(e => e.UnicodeCode).HasColumnName("unicode_code");
            entity.Property(e => e.WanikaniId).HasColumnName("wanikani_id");
        });

        modelBuilder.Entity<KanjiDecomposition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kanji_decomposition_pkey");
            entity.ToTable("kanji_decomposition");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ComponentKanjiId).HasColumnName("component_kanji_id");
            entity.Property(e => e.ComponentRadicalId).HasColumnName("component_radical_id");
            entity.Property(e => e.ComponentType).HasColumnName("component_type");
            entity.Property(e => e.DepthLevel).HasDefaultValue((short)1).HasColumnName("depth_level");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.ParentKanjiId).HasColumnName("parent_kanji_id");
            entity.Property(e => e.RelationType).HasColumnName("relation_type");
            entity.HasOne(d => d.ComponentKanji).WithMany(p => p.KanjiDecompositionComponentKanjis).HasForeignKey(d => d.ComponentKanjiId).HasConstraintName("kanji_decomposition_component_kanji_id_fkey");
            entity.HasOne(d => d.ComponentRadical).WithMany(p => p.KanjiDecompositions).HasForeignKey(d => d.ComponentRadicalId).HasConstraintName("kanji_decomposition_component_radical_id_fkey");
            entity.HasOne(d => d.ParentKanji).WithMany(p => p.KanjiDecompositionParentKanjis).HasForeignKey(d => d.ParentKanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("kanji_decomposition_parent_kanji_id_fkey");
        });

        modelBuilder.Entity<KanjiMeaning>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kanji_meaning_pkey");
            entity.ToTable("kanji_meaning");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Gloss).HasColumnName("gloss");
            entity.Property(e => e.IdLanguage).HasColumnName("id_language");
            entity.Property(e => e.IsPrincipal).HasColumnName("is_principal");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.HasOne(d => d.IdLanguageNavigation).WithMany(p => p.KanjiMeanings).HasForeignKey(d => d.IdLanguage).HasConstraintName("kanji_meaning_id_language_fk");
            entity.HasOne(d => d.Kanji).WithMany(p => p.KanjiMeanings).HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("kanji_meaning_kanji_id_fkey");
        });

        modelBuilder.Entity<KanjiRadical>(entity =>
        {
            entity.HasKey(e => new { e.KanjiId, e.RadicalId }).HasName("kanji_radical_pkey");
            entity.ToTable("kanji_radical");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.Property(e => e.RadicalId).HasColumnName("radical_id");
            entity.Property(e => e.ImportanceOrd).HasColumnName("importance_ord");
            entity.Property(e => e.Position).HasColumnName("position");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.HasOne(d => d.Kanji).WithMany(p => p.KanjiRadicals).HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("kanji_radical_kanji_id_fkey");
            entity.HasOne(d => d.Radical).WithMany(p => p.KanjiRadicals).HasForeignKey(d => d.RadicalId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("kanji_radical_radical_id_fkey");
        });

        modelBuilder.Entity<KanjiReading>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kanji_reading_pkey");
            entity.ToTable("kanji_reading");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.Property(e => e.ReadingKana).HasColumnName("reading_kana");
            entity.Property(e => e.ReadingRomaji).HasColumnName("reading_romaji");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.HasOne(d => d.Kanji).WithMany(p => p.KanjiReadings).HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("kanji_reading_kanji_id_fkey");
        });

        modelBuilder.Entity<KanjiSimilarity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kanji_similaridade_pk");
            entity.ToTable("kanji_similarity");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('kaji_similaridade_id_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.IdKanji).HasColumnName("id_kanji");
            entity.Property(e => e.IdKanjiSimilar).HasColumnName("id_kanji_similar");
            entity.HasOne(d => d.IdKanjiNavigation).WithMany(p => p.KanjiSimilarityIdKanjiNavigations).HasForeignKey(d => d.IdKanji).HasConstraintName("kanji_similaridade_id_kanji");
            entity.HasOne(d => d.IdKanjiSimilarNavigation).WithMany(p => p.KanjiSimilarityIdKanjiSimilarNavigations).HasForeignKey(d => d.IdKanjiSimilar).HasConstraintName("kanji_similaridade_id_kanji_similar");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("language_id_pk");
            entity.ToTable("language");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LanguageCode).HasColumnName("language_code");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("lesson_id_pk");
            entity.ToTable("lesson");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IdKanji).HasColumnName("id_kanji");
            entity.Property(e => e.IdRadical).HasColumnName("id_radical");
            entity.Property(e => e.ImagePath).HasColumnName("image_path");
            entity.Property(e => e.VocabularyId).HasColumnName("vocabulary_id");
            entity.HasOne(d => d.IdKanjiNavigation).WithMany(p => p.Lessons).HasForeignKey(d => d.IdKanji).HasConstraintName("lesson_id_kanji_fk");
            entity.HasOne(d => d.IdRadicalNavigation).WithMany(p => p.Lessons).HasForeignKey(d => d.IdRadical).HasConstraintName("lesson_id_radical_fk");
            entity.HasOne(d => d.Vocabulary).WithMany(p => p.Lessons).HasForeignKey(d => d.VocabularyId).HasConstraintName("lesson_id_vocabulario_fk");
        });

        modelBuilder.Entity<Level>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("level_id_pk");
            entity.ToTable("level");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.Description).HasColumnName("description");
        });

        modelBuilder.Entity<LevelItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("nivel_iten_id_pk");
            entity.ToTable("level_item");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('nivel_iten_id_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.IdNivelItenObjeto).HasColumnName("id_nivel_iten_objeto");
            entity.Property(e => e.IdObjeto).HasColumnName("id_objeto");
            entity.HasOne(d => d.IdNivelNavigation).WithMany(p => p.LevelItems).HasForeignKey(d => d.IdNivel).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("nivel_iten_id_nivel_fk");
            entity.HasOne(d => d.IdNivelItenObjetoNavigation).WithMany(p => p.LevelItems).HasForeignKey(d => d.IdNivelItenObjeto).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("nivel_iten_id_nivel_iten_objeto_fk");
        });

        modelBuilder.Entity<LevelItemType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("nivel_iten_objeto_id_pk");
            entity.ToTable("level_item_type");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('nivel_iten_objetoid_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
        });

        modelBuilder.Entity<Radical>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("radical_pkey");
            entity.ToTable("radical");
            entity.HasIndex(e => e.WanikaniId, "idx_radical_wanikani_id");
            entity.HasIndex(e => e.UnicodeCode, "radical_unicode_code_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.KangxiNumber).HasColumnName("kangxi_number");
            entity.Property(e => e.Literal).HasColumnName("literal");
            entity.Property(e => e.PathImg).HasColumnType("char").HasColumnName("path_img");
            entity.Property(e => e.StrokeCount).HasColumnName("stroke_count");
            entity.Property(e => e.UnicodeCode).HasColumnName("unicode_code");
            entity.Property(e => e.WanikaniId).HasColumnName("wanikani_id");

            entity.HasMany(d => d.Kanjis).WithMany(p => p.Radicals)
                .UsingEntity<Dictionary<string, object>>(
                    "RadicalKanjiMap",
                    r => r.HasOne<Kanji>().WithMany().HasForeignKey("KanjiId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("radical_kanji_map_kanji_id_fkey"),
                    l => l.HasOne<Radical>().WithMany().HasForeignKey("RadicalId").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("radical_kanji_map_radical_id_fkey"),
                    j =>
                    {
                        j.HasKey("RadicalId", "KanjiId").HasName("radical_kanji_map_pkey");
                        j.ToTable("radical_kanji_map");
                        j.IndexerProperty<int>("RadicalId").HasColumnName("radical_id");
                        j.IndexerProperty<int>("KanjiId").HasColumnName("kanji_id");
                    });
        });

        modelBuilder.Entity<RadicalMeaning>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("radical_reading_id_pk");
            entity.ToTable("radical_meaning");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("descrition");
            entity.Property(e => e.IdLanguage).HasColumnName("id_language");
            entity.Property(e => e.IdRadical).HasColumnName("id_radical");
            entity.HasOne(d => d.IdLanguageNavigation).WithMany(p => p.RadicalMeanings).HasForeignKey(d => d.IdLanguage).HasConstraintName("radical_reading_id_language_fk");
            entity.HasOne(d => d.IdRadicalNavigation).WithMany(p => p.RadicalMeanings).HasForeignKey(d => d.IdRadical).HasConstraintName("radical_reading_id_radical_fk");
        });

        modelBuilder.Entity<ReviewHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("historico_revisao_pkey");
            entity.ToTable("review_history");
            entity.HasIndex(e => new { e.UserId, e.ReviewedAt }, "idx_usuario_historico");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('historico_revisao_id_historico_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.CardId).HasColumnName("card_id");
            entity.Property(e => e.IntervalUsed).HasColumnName("interval_used");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.ReviewedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("reviewed_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Card).WithMany(p => p.ReviewHistories).HasForeignKey(d => d.CardId).HasConstraintName("fk_cartao_historico");
            entity.HasOne(d => d.User).WithMany(p => p.ReviewHistories).HasForeignKey(d => d.UserId).HasConstraintName("fk_usuario_historico");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("usuarios_pkey");
            entity.ToTable("user");
            entity.HasIndex(e => e.Email, "usuarios_email_key").IsUnique();
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('usuarios_id_usuario_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.Email).HasMaxLength(255).HasColumnName("email");
            entity.Property(e => e.RegisteredAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("registered_at");
            entity.Property(e => e.Username).HasMaxLength(100).HasColumnName("username");
        });

        modelBuilder.Entity<UserNote>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("usuario_anotacao_pkey");
            entity.ToTable("user_note");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('usuario_anotacao_id_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("created_at");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.Property(e => e.RadicalId).HasColumnName("radical_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Kanji).WithMany(p => p.UserNotes).HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_anotacao_id_kanji_fkey");
            entity.HasOne(d => d.Radical).WithMany(p => p.UserNotes).HasForeignKey(d => d.RadicalId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_anotacao_id_radical_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.UserNotes).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_anotacao_id_usuario_fkey");
        });

        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("progresso_usuario_pkey");
            entity.ToTable("user_progress");
            entity.HasIndex(e => e.NextReviewAt, "idx_proxima_revisao");
            entity.HasIndex(e => new { e.UserId, e.CardId }, "idx_usuario_cartao_progresso");
            entity.HasIndex(e => new { e.UserId, e.CardId }, "progresso_usuario_id_usuario_id_cartao_key").IsUnique();
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('progresso_usuario_id_progresso_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.CardId).HasColumnName("card_id");
            entity.Property(e => e.ConsecutiveCorrectCount).HasDefaultValue(0).HasColumnName("consecutive_correct_count");
            entity.Property(e => e.EaseFactor).HasPrecision(4, 2).HasDefaultValueSql("2.50").HasColumnName("ease_factor");
            entity.Property(e => e.Interval).HasDefaultValue(0).HasColumnName("interval");
            entity.Property(e => e.LastReviewedAt).HasColumnName("last_reviewed_at");
            entity.Property(e => e.NextReviewAt).HasColumnName("next_review_at");
            entity.Property(e => e.ReviewCount).HasDefaultValue(0).HasColumnName("review_count");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Card).WithMany(p => p.UserProgresses).HasForeignKey(d => d.CardId).HasConstraintName("fk_cartao_progresso");
            entity.HasOne(d => d.User).WithMany(p => p.UserProgresses).HasForeignKey(d => d.UserId).HasConstraintName("fk_usuario_progresso");
        });

        modelBuilder.Entity<UserSynonym>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("usuario_sinonimo_kanji_radical_pkey");
            entity.ToTable("user_synonym");
            entity.Property(e => e.Id).HasDefaultValueSql("nextval('usuario_sinonimo_kanji_radical_id_seq'::regclass)").HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnName("created_at");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.Property(e => e.RadicalId).HasColumnName("radical_id");
            entity.Property(e => e.SynonymText).HasColumnName("synonym_text");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.Kanji).WithMany(p => p.UserSynonyms).HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_sinonimo_kanji_radical_id_kanji_fkey");
            entity.HasOne(d => d.Radical).WithMany(p => p.UserSynonyms).HasForeignKey(d => d.RadicalId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_sinonimo_kanji_radical_id_radical_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.UserSynonyms).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("usuario_sinonimo_kanji_radical_id_usuario_fkey");
        });

        modelBuilder.Entity<Vocabulary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vocabulary_pkey");
            entity.ToTable("vocabulary");
            entity.HasIndex(e => e.WanikaniId, "vocabulary_wanikani_id_key").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Characters).HasColumnName("characters");
            entity.Property(e => e.Level).HasColumnName("level");
            //entity.Property(e => e.MeaningMnemonic).HasColumnName("meaning_mnemonic");
            //entity.Property(e => e.ReadingMnemonic).HasColumnName("reading_mnemonic");
            entity.Property(e => e.WanikaniId).HasColumnName("wanikani_id");
        });

        modelBuilder.Entity<VocabularyComposition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vocabulary_composition_pkey");
            entity.ToTable("vocabulary_composition");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.VocabularyId).HasColumnName("vocabulary_id");
            entity.Property(e => e.KanjiId).HasColumnName("kanji_id");
            entity.HasOne(d => d.Kanji).WithMany().HasForeignKey(d => d.KanjiId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("vocabulary_composition_kanji_id_fkey");
            entity.HasOne(d => d.Vocabulary).WithMany().HasForeignKey(d => d.VocabularyId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("vocabulary_composition_vocabulary_id_fkey");
        });

        modelBuilder.Entity<VocabularyContextSentence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vocabulary_context_sentence_pkey");
            entity.ToTable("vocabulary_context_sentence");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.En).HasColumnName("en");
            entity.Property(e => e.Jp).HasColumnName("jp");
            entity.Property(e => e.VocabularyId).HasColumnName("vocabulary_id");

            // FORÇAR O MAPEAMENTO DA COLUNA NA TABELA DE FRASES
            entity.Property(e => e.LanguageId).HasColumnName("language_id");

            entity.HasOne(d => d.Vocabulary).WithMany(p => p.VocabularyContextSentences)
                .HasForeignKey(d => d.VocabularyId)
                .HasConstraintName("vocabulary_context_sentence_vocabulary_id_fkey");

            entity.HasOne(d => d.Language).WithMany()
                .HasForeignKey(d => d.LanguageId)
                .HasConstraintName("fk_sentence_language");
        });

        modelBuilder.Entity<VocabularyMeaning>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vocabulary_meaning_pkey");
            entity.ToTable("vocabulary_meaning");
            entity.Property(e => e.Id).HasColumnName("id");

            // Mapeamento explícito para evitar discrepâncias entre nomes de propriedade C# e colunas no banco
            entity.Property(e => e.VocabularyId).HasColumnName("vocabulary_id");

            // Ajustado para seguir padrão usado nas outras tabelas (ex: 'id_language')
            entity.Property(e => e.LanguageId).HasColumnName("id_language");

            // Mapear colunas restantes para snake_case no banco
            entity.Property(e => e.Meaning).HasColumnName("meaning");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary");

            entity.HasOne(d => d.Vocabulary)
                .WithMany(p => p.VocabularyMeanings)
                .HasForeignKey(d => d.VocabularyId)
                .HasConstraintName("vocabulary_meaning_vocabulary_id_fkey");

            entity.HasOne(d => d.Language)
                .WithMany()
                .HasForeignKey(d => d.LanguageId)
                .HasConstraintName("vocabulary_meaning_language_id_fkey");
        });

        modelBuilder.Entity<VocabularyReading>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vocabulary_reading_pkey");
            entity.ToTable("vocabulary_reading");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IsPrimary).HasColumnName("is_primary").HasColumnType("boolean");
            entity.Property(e => e.Reading).HasColumnName("reading");
            entity.Property(e => e.VocabularyId).HasColumnName("vocabulary_id");
            entity.HasOne(d => d.Vocabulary).WithMany(p => p.VocabularyReadings).HasForeignKey(d => d.VocabularyId).HasConstraintName("vocabulary_reading_vocabulary_id_fkey");
        });

        modelBuilder.HasSequence("kaji_similaridade_id_seq");
        modelBuilder.HasSequence("language_id_seq");
        modelBuilder.HasSequence("lesson_id_seq");
        modelBuilder.HasSequence("level_id_seq");
        modelBuilder.HasSequence("nivel_iten_id_seq");
        modelBuilder.HasSequence("nivel_iten_objetoid_seq");
        modelBuilder.HasSequence("radical_reading_id_seq");
        modelBuilder.HasSequence("usuario_anotacao_id_seq");
        modelBuilder.HasSequence("usuario_sinonimo_kanji_radical_id_seq");

        OnModelCreatingPartial(modelBuilder);

        modelBuilder.Entity<SyntaxHighlight>(entity =>
        {
            entity.Property(e => e.IsBold).HasDefaultValue(false);
            entity.Property(e => e.IsItalic).HasDefaultValue(false);
            entity.Property(e => e.IsUnderline).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
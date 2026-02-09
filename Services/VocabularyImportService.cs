using System.Xml;
using Npgsql;
using System.Text.Json;

namespace Denkiishi_v2.Services
{
    public class VocabularyImportService
    {
        private readonly string _connectionString;

        public VocabularyImportService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task ImportToStagingAsync(string xmlPath)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null,
                MaxCharactersFromEntities = 0
            };

            // Comando COPY incluindo jlpt_level e frequency_rank
            using (var writer = conn.BeginBinaryImport("COPY stage_jmdict_raw (ent_seq, kanji_text, reading_text, meanings_json, part_of_speech, misc_tags, jlpt_level, frequency_rank) FROM STDIN (FORMAT BINARY)"))
            {
                using var reader = XmlReader.Create(xmlPath, settings);
                while (reader.Read())
                {
                    if (reader.IsStartElement() && reader.Name == "entry")
                    {
                        var entryXml = reader.ReadOuterXml();
                        var doc = new XmlDocument();
                        doc.XmlResolver = null;
                        doc.LoadXml(entryXml);

                        int entSeq = int.Parse(doc.SelectSingleNode("//ent_seq")?.InnerText ?? "0");
                        string kanji = doc.SelectSingleNode("//keb")?.InnerText ?? "";
                        string reading = doc.SelectSingleNode("//reb")?.InnerText ?? "";

                        // --- NOVA LÓGICA DE JLPT E FREQUÊNCIA ---
                        int jlpt = 0;
                        int freqRank = 999999;

                        // Buscamos todas as tags de prioridade na entrada inteira
                        var priorityNodes = doc.GetElementsByTagName("ke_pri").Cast<XmlNode>()
                                            .Concat(doc.GetElementsByTagName("re_pri").Cast<XmlNode>());

                        foreach (XmlNode node in priorityNodes)
                        {
                            string val = node.InnerText;

                            // Captura JLPT: "jlpt-n5" -> 5
                            if (val.StartsWith("jlpt-n"))
                            {
                                if (int.TryParse(val.Replace("jlpt-n", ""), out int level))
                                {
                                    // Se houver conflito, pegamos o nível mais alto (ex: N5 é 5)
                                    if (jlpt == 0 || level < jlpt) jlpt = level;
                                }
                            }

                            // Captura Rank nfXX (nf01 = bloco 1, nf02 = bloco 2...)
                            if (val.StartsWith("nf"))
                            {
                                if (int.TryParse(val.Replace("nf", ""), out int block))
                                {
                                    if (block < freqRank) freqRank = block;
                                }
                            }
                            // Se tiver tags como news1, ichi1, spec1, marcamos como prioridade alta (50)
                            else if (val.EndsWith("1") && freqRank > 50)
                            {
                                freqRank = 50;
                            }
                        }

                        var meanings = doc.SelectNodes("//gloss").Cast<XmlNode>().Select(n => n.InnerText).ToList();
                        var posTags = doc.SelectNodes("//pos").Cast<XmlNode>().Select(n => n.InnerText).ToList();
                        var miscTags = doc.SelectNodes("//misc").Cast<XmlNode>().Select(n => n.InnerText).ToList();

                        // Gravação Binária
                        await writer.StartRowAsync();
                        await writer.WriteAsync(entSeq, NpgsqlTypes.NpgsqlDbType.Integer);
                        await writer.WriteAsync(kanji, NpgsqlTypes.NpgsqlDbType.Text);
                        await writer.WriteAsync(reading, NpgsqlTypes.NpgsqlDbType.Text);
                        await writer.WriteAsync(JsonSerializer.Serialize(meanings), NpgsqlTypes.NpgsqlDbType.Jsonb);
                        await writer.WriteAsync(posTags.ToArray(), NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
                        await writer.WriteAsync(miscTags.ToArray(), NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
                        await writer.WriteAsync(jlpt, NpgsqlTypes.NpgsqlDbType.Integer);
                        await writer.WriteAsync(freqRank, NpgsqlTypes.NpgsqlDbType.Integer);
                    }
                }
                await writer.CompleteAsync();
            }
        }
    }
}
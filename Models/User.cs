using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime? RegisteredAt { get; set; }

    public DateOnly? BirthDate { get; set; }

    public virtual ICollection<Deck> Decks { get; set; } = new List<Deck>();

    public virtual ICollection<ReviewHistory> ReviewHistories { get; set; } = new List<ReviewHistory>();

    public virtual ICollection<UserNote> UserNotes { get; set; } = new List<UserNote>();

    public virtual ICollection<UserProgress> UserProgresses { get; set; } = new List<UserProgress>();

    public virtual ICollection<UserSynonym> UserSynonyms { get; set; } = new List<UserSynonym>();
}

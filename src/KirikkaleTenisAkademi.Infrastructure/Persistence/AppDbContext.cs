using KirikkaleTenisAkademi.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KirikkaleTenisAkademi.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Coach> Coaches { get; set; }
        
        // YENİ TABLOLAR
        public DbSet<LessonPacket> LessonPackets { get; set; }       // Paketler
        public DbSet<LessonBooking> LessonBookings { get; set; }     // Rezervasyonlar
        public DbSet<CoachUnavailability> CoachUnavailabilities { get; set; } // Koç İzinleri
        public DbSet<Tournament> Tournaments { get; set; }           // Turnuvalar
        public DbSet<Match> Matches { get; set; }                    // Maçlar

        // ESKİ TABLO SİLİNDİ: public DbSet<LessonSlot> LessonSlots { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // İlişkileri tanımlayalım (Fluent API)
            
            // Maç - Oyuncu İlişkisi (İki taraf da AppUser olduğu için karışmasın)
            builder.Entity<Match>()
                .HasOne(m => m.Player1)
                .WithMany()
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.Restrict); // Oyuncu silinirse maç silinmesin

            builder.Entity<Match>()
                .HasOne(m => m.Player2)
                .WithMany()
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Booking İlişkileri
            builder.Entity<LessonBooking>()
                .HasOne(b => b.Student)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.StudentId);

            builder.Entity<LessonBooking>()
                .HasOne(b => b.Coach)
                .WithMany()
                .HasForeignKey(b => b.CoachId);
        }
    }
}
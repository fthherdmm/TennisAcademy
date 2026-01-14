using KirikkaleTenisAkademi.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KirikkaleTenisAkademi.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Coach> Coaches { get; set; }
        public DbSet<LessonPacket> LessonPackets { get; set; }
        public DbSet<LessonBooking> LessonBookings { get; set; }
        public DbSet<CoachUnavailability> CoachUnavailabilities { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<GroupLesson> GroupLessons { get; set; }
        public DbSet<GroupLessonRegistration> GroupLessonRegistrations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. MAÇ İLİŞKİLERİ (Zaten doğruydu, koruyoruz)
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

            // =========================================================
            // 🔥 2. HATA VEREN KISIM (LESSON BOOKING) DÜZELTİLDİ 🔥
            // =========================================================
            // MSSQL'de "Cascade" döngüsü olmaması için bunları "Restrict" yapıyoruz.
            
            builder.Entity<LessonBooking>()
                .HasOne(b => b.Student)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.StudentId)
                .OnDelete(DeleteBehavior.Restrict); // Öğrenci silinirse hata ver (önce rezervasyonu silmelisin)

            builder.Entity<LessonBooking>()
                .HasOne(b => b.Coach)
                .WithMany() // Coach tarafında ICollection<LessonBooking> yoksa boş kalabilir
                .HasForeignKey(b => b.CoachId)
                .OnDelete(DeleteBehavior.Restrict); // Antrenör silinirse hata ver

            // 3. GRUP DERSİ İLİŞKİLERİ
            builder.Entity<GroupLesson>()
                .HasOne(g => g.Coach)
                .WithMany()
                .HasForeignKey(g => g.CoachId)
                .OnDelete(DeleteBehavior.Restrict); // Güvenlik için bunu da Restrict yaptık

            // 4. GRUP KAYIT İLİŞKİLERİ (Çoka-Çok Tablosu)
            builder.Entity<GroupLessonRegistration>()
                .HasOne(r => r.GroupLesson)
                .WithMany(g => g.Registrations)
                .HasForeignKey(r => r.GroupLessonId)
                .OnDelete(DeleteBehavior.Cascade); // Ders silinirse kayıtlar silinebilir (Sorun yok)

            builder.Entity<GroupLessonRegistration>()
                .HasOne(r => r.Student)
                .WithMany(s => s.GroupRegistrations)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict); // Öğrenci silinirse kayıtlar kalsın (Güvenlik)

            // 5. Diğer Ayarlar
            builder.Entity<LessonPacket>()
                .Property(p => p.Id)
                .UseIdentityColumn();
        }
    }
}
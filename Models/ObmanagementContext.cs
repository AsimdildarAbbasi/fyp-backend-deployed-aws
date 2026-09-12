using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OBManagementAPI.Models;

public partial class ObmanagementContext : DbContext
{
    public ObmanagementContext()
    {
    }

    public ObmanagementContext(DbContextOptions<ObmanagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<BuildingFloor> BuildingFloors { get; set; }

    public virtual DbSet<FacultyMemberOffice> FacultyMemberOffices { get; set; }

    public virtual DbSet<FacultyTracking> FacultyTrackings { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Office> Offices { get; set; }

    public virtual DbSet<OfficeBoyAssignedFloor> OfficeBoyAssignedFloors { get; set; }

    public virtual DbSet<Task> Tasks { get; set; }

    public virtual DbSet<ArrivalDepartureTask> ArrivalDepartureTasks { get; set; }

    public virtual DbSet<Geofence> Geofences { get; set; }

    public virtual DbSet<TaskCategory> TaskCategories { get; set; }

    public virtual DbSet<FacultyGeofenceState> FacultyGeofenceStates { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=(local)\\SQLEXPRESS;Database=OBManagement;User Id=sa;Password=admin123;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Account__3214EC07BB7B523E");

            entity.ToTable("Account");

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255);
        });

        modelBuilder.Entity<BuildingFloor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Building__3214EC070E414C38");

            entity.ToTable("BuildingFloor");

            entity.Property(e => e.Number).HasMaxLength(50);
        });

        modelBuilder.Entity<FacultyMemberOffice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FacultyM__3214EC07CEFD6BEC");

            entity.ToTable("FacultyMemberOffice");

            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.FacultyAccount).WithMany(p => p.FacultyMemberOffices)
                .HasForeignKey(d => d.FacultyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FacultyMe__Facul__5165187F");

            entity.HasOne(d => d.Office).WithMany(p => p.FacultyMemberOffices)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__FacultyMe__Offic__52593CB8");
        });

        modelBuilder.Entity<FacultyTracking>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("FacultyTracking");

            entity.Property(e => e.Latitude).HasColumnType("float");
            entity.Property(e => e.Longitude).HasColumnType("float");
            entity.Property(e => e.RecordedAt).HasColumnType("datetime");

            entity.HasOne(d => d.FacultyAccount).WithMany(p => p.FacultyTrackings)
                .HasForeignKey(d => d.FacultyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FacultyTracking_Account");

            entity.HasOne(d => d.Geofence).WithMany(p => p.FacultyTrackings)
                .HasForeignKey(d => d.GeofenceId)
                .HasConstraintName("FK_FacultyTracking_Geofence");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Location__3214EC076A76558D");

            entity.ToTable("Location");

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Office>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Office__3214EC07AE5D7385");

            entity.ToTable("Office");

            entity.Property(e => e.OfficeName).HasMaxLength(100);

            entity.HasOne(d => d.BuildingFloor).WithMany(p => p.Offices)
                .HasForeignKey(d => d.BuildingFloorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Office__Building__4E88ABD4");
        });

        modelBuilder.Entity<OfficeBoyAssignedFloor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OfficeBo__3214EC072AB34896");

            entity.ToTable("OfficeBoyAssignedFloors");

            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Floor).WithMany(p => p.OfficeBoyAssignedFloors)
                .HasForeignKey(d => d.FloorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfficeBoy__Floor__5535A963");

            entity.HasOne(d => d.OfficeBoyAccount).WithMany(p => p.OfficeBoyAssignedFloors)
                .HasForeignKey(d => d.OfficeBoyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfficeBoy__Offic__571DF1D5");

            entity.HasOne(d => d.Office).WithMany(p => p.OfficeBoyAssignedFloors)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OfficeBoy__Offic__5629CD9C");
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Task__3214EC07C2FACE9A");

            entity.ToTable("Task");

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Remarks).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TaskTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TriggerType)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.FacultyAccount).WithMany(p => p.TaskFacultyAccounts)
                .HasForeignKey(d => d.FacultyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Task__FacultyAcc__5CD6CB2B");

            entity.HasOne(d => d.Location).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Task__LocationId__5EBF139D");

            entity.HasOne(d => d.OfficeBoyAccount).WithMany(p => p.TaskOfficeBoyAccounts)
                .HasForeignKey(d => d.OfficeBoyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Task__OfficeBoyA__5DCAEF64");

            entity.HasOne(d => d.CurrentLocation).WithMany()
                .HasForeignKey(d => d.CurrentLocationId)
                .HasConstraintName("FK__Task__CurrentLocation");

            entity.HasOne(d => d.Geofence).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.GeofenceId)
                .HasConstraintName("FK_Task_Geofence");

            entity.HasOne(d => d.TaskCategory).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.TaskCategoryId)
                .HasConstraintName("FK_Task_TaskCategory");
        });

        modelBuilder.Entity<ArrivalDepartureTask>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("ArrivalDepartureTasks");
        });

        modelBuilder.Entity<Geofence>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Geofence");

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.CenterLatitude).HasColumnType("float");
            entity.Property(e => e.CenterLongitude).HasColumnType("float");
            entity.Property(e => e.RadiusMeters).HasColumnType("float");
        });

        modelBuilder.Entity<TaskCategory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("TaskCategory");

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<FacultyGeofenceState>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("FacultyGeofenceState");

            entity.Property(e => e.LastUpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.FacultyAccount).WithMany(p => p.FacultyGeofenceStates)
                .HasForeignKey(d => d.FacultyAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FacultyGeofenceState_Account");

            entity.HasOne(d => d.Geofence).WithMany(p => p.FacultyGeofenceStates)
                .HasForeignKey(d => d.GeofenceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FacultyGeofenceState_Geofence");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

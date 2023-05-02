﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pal.Client.Database;

#nullable disable

namespace Pal.Client.Database.Migrations
{
    [DbContext(typeof(PalClientContext))]
    [Migration("20230218112804_AddImportedAndSinceVersionToClientLocation")]
    partial class AddImportedAndSinceVersionToClientLocation
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.3");

            modelBuilder.Entity("ClientLocationImportHistory", b =>
                {
                    b.Property<Guid>("ImportedById")
                        .HasColumnType("TEXT");

                    b.Property<int>("ImportedLocationsLocalId")
                        .HasColumnType("INTEGER");

                    b.HasKey("ImportedById", "ImportedLocationsLocalId");

                    b.HasIndex("ImportedLocationsLocalId");

                    b.ToTable("LocationImports", (string)null);
                });

            modelBuilder.Entity("Pal.Client.Database.ClientLocation", b =>
                {
                    b.Property<int>("LocalId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Imported")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Seen")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SinceVersion")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<ushort>("TerritoryType")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<float>("X")
                        .HasColumnType("REAL");

                    b.Property<float>("Y")
                        .HasColumnType("REAL");

                    b.Property<float>("Z")
                        .HasColumnType("REAL");

                    b.HasKey("LocalId");

                    b.ToTable("Locations");
                });

            modelBuilder.Entity("Pal.Client.Database.ImportHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ExportedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ImportedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("RemoteUrl")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Imports");
                });

            modelBuilder.Entity("Pal.Client.Database.RemoteEncounter", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AccountId")
                        .IsRequired()
                        .HasMaxLength(13)
                        .HasColumnType("TEXT");

                    b.Property<int>("ClientLocationId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ClientLocationId");

                    b.ToTable("RemoteEncounters");
                });

            modelBuilder.Entity("ClientLocationImportHistory", b =>
                {
                    b.HasOne("Pal.Client.Database.ImportHistory", null)
                        .WithMany()
                        .HasForeignKey("ImportedById")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Pal.Client.Database.ClientLocation", null)
                        .WithMany()
                        .HasForeignKey("ImportedLocationsLocalId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Pal.Client.Database.RemoteEncounter", b =>
                {
                    b.HasOne("Pal.Client.Database.ClientLocation", "ClientLocation")
                        .WithMany("RemoteEncounters")
                        .HasForeignKey("ClientLocationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ClientLocation");
                });

            modelBuilder.Entity("Pal.Client.Database.ClientLocation", b =>
                {
                    b.Navigation("RemoteEncounters");
                });
#pragma warning restore 612, 618
        }
    }
}

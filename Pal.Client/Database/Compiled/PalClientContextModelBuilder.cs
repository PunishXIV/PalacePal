﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#pragma warning disable 219, 612, 618
#nullable enable

namespace Pal.Client.Database.Compiled
{
    public partial class PalClientContextModel
    {
        partial void Initialize()
        {
            var clientLocationImportHistory = ClientLocationImportHistoryEntityType.Create(this);
            var clientLocation = ClientLocationEntityType.Create(this);
            var importHistory = ImportHistoryEntityType.Create(this);
            var remoteEncounter = RemoteEncounterEntityType.Create(this);

            ClientLocationImportHistoryEntityType.CreateForeignKey1(clientLocationImportHistory, importHistory);
            ClientLocationImportHistoryEntityType.CreateForeignKey2(clientLocationImportHistory, clientLocation);
            RemoteEncounterEntityType.CreateForeignKey1(remoteEncounter, clientLocation);

            ClientLocationEntityType.CreateSkipNavigation1(clientLocation, importHistory, clientLocationImportHistory);
            ImportHistoryEntityType.CreateSkipNavigation1(importHistory, clientLocation, clientLocationImportHistory);

            ClientLocationImportHistoryEntityType.CreateAnnotations(clientLocationImportHistory);
            ClientLocationEntityType.CreateAnnotations(clientLocation);
            ImportHistoryEntityType.CreateAnnotations(importHistory);
            RemoteEncounterEntityType.CreateAnnotations(remoteEncounter);

            AddAnnotation("ProductVersion", "7.0.3");
        }
    }
}

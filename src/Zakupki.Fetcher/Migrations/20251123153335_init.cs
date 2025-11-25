using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zakupki.Fetcher.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasLifetimeAccess = table.Column<bool>(type: "bit", nullable: false),
                    CompanyInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntryName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Region = table.Column<byte>(type: "tinyint", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RegNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: true),
                    SchemeVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PurchaseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContractSubject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CurrencyName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Okpd2Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Okpd2Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Href = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PublishDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Region = table.Column<byte>(type: "tinyint", nullable: false),
                    PurchaseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublishDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Href = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PlacingWayCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EtpName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EtpUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PurchaseObjectInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Okpd2Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Okpd2Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    KvrCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KvrName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectingEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Okpd2Codes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Okpd2Codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUserRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Region = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUserRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationUserRegions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expires = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserQueryVectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Vector = table.Column<SqlVector<float>>(type: "vector(1024)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQueryVectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserQueryVectors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteNotices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteNotices_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionScore = table.Column<double>(type: "float", nullable: true),
                    Recommended = table.Column<bool>(type: "bit", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeAnalyses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoticeAnalyses_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vector = table.Column<SqlVector<float>>(type: "vector(1024)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeEmbeddings_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VersionReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeVersions_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NoticeVersions_Notices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUserOkpd2Codes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Okpd2CodeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUserOkpd2Codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationUserOkpd2Codes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationUserOkpd2Codes_Okpd2Codes_Okpd2CodeId",
                        column: x => x.Okpd2CodeId,
                        principalTable: "Okpd2Codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedContentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentKindCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DocumentKindName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BinaryContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    MarkdownContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsertedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeAttachments_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeSearchVectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregatedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbeddingVector = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeSearchVectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeSearchVectors_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectingStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CollectingEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BiddingDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SummarizingDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FirstPartsDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmissionProcedureDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SecondPartsDateRaw = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureWindows_NoticeVersions_NoticeVersionId",
                        column: x => x.NoticeVersionId,
                        principalTable: "NoticeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignatureType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttachmentSignatures_NoticeAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "NoticeAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserOkpd2Codes_Okpd2CodeId",
                table: "ApplicationUserOkpd2Codes",
                column: "Okpd2CodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserOkpd2Codes_UserId_Okpd2CodeId",
                table: "ApplicationUserOkpd2Codes",
                columns: new[] { "UserId", "Okpd2CodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserRegions_UserId_Region",
                table: "ApplicationUserRegions",
                columns: new[] { "UserId", "Region" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentSignatures_AttachmentId",
                table: "AttachmentSignatures",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Contracts_ExternalId",
                table: "Contracts",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteNotices_NoticeId",
                table: "FavoriteNotices",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "UX_FavoriteNotices_User_Notice",
                table: "FavoriteNotices",
                columns: new[] { "UserId", "NoticeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAnalyses_UserId",
                table: "NoticeAnalyses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeAnalyses_Notice_User",
                table: "NoticeAnalyses",
                columns: new[] { "NoticeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_DocKindCode",
                table: "NoticeAttachments",
                column: "DocumentKindCode");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_FileName",
                table: "NoticeAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAttachments_NoticeVersionId",
                table: "NoticeAttachments",
                column: "NoticeVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeAttachments_ContentId_Version",
                table: "NoticeAttachments",
                columns: new[] { "PublishedContentId", "NoticeVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeEmbeddings_NoticeId",
                table: "NoticeEmbeddings",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notices_CollectingEnd",
                table: "Notices",
                column: "CollectingEnd");

            migrationBuilder.CreateIndex(
                name: "IX_Notices_PurchaseNumber",
                table: "Notices",
                column: "PurchaseNumber");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeSearchVectors_NoticeVersionId",
                table: "NoticeSearchVectors",
                column: "NoticeVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeVersions_ImportBatchId",
                table: "NoticeVersions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeVersions_NoticeId",
                table: "NoticeVersions",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "UX_NoticeVersions_External_Version",
                table: "NoticeVersions",
                columns: new[] { "ExternalId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Okpd2Codes_Code",
                table: "Okpd2Codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_CollectingEnd",
                table: "ProcedureWindows",
                column: "CollectingEnd");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_CollectingStart",
                table: "ProcedureWindows",
                column: "CollectingStart");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureWindows_NoticeVersionId",
                table: "ProcedureWindows",
                column: "NoticeVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQueryVectors_User_CreatedAt",
                table: "UserQueryVectors",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationUserOkpd2Codes");

            migrationBuilder.DropTable(
                name: "ApplicationUserRegions");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AttachmentSignatures");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "FavoriteNotices");

            migrationBuilder.DropTable(
                name: "NoticeAnalyses");

            migrationBuilder.DropTable(
                name: "NoticeEmbeddings");

            migrationBuilder.DropTable(
                name: "NoticeSearchVectors");

            migrationBuilder.DropTable(
                name: "ProcedureWindows");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "UserQueryVectors");

            migrationBuilder.DropTable(
                name: "Okpd2Codes");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "NoticeAttachments");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "NoticeVersions");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "Notices");
        }
    }
}

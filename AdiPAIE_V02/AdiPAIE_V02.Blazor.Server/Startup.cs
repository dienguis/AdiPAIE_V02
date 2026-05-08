using AdiPAIE_V02.Blazor.Server.Services;
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Blazor.ApplicationBuilder;
using DevExpress.ExpressApp.Blazor.Services;

using DevExpress.ExpressApp.Security;

using DevExpress.Persistent.BaseImpl.PermissionPolicy;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;


namespace AdiPAIE_V02.Blazor.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(typeof(Microsoft.AspNetCore.SignalR.HubConnectionHandler<>), typeof(ProxyHubConnectionHandler<>));

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddHttpContextAccessor();
            services.AddScoped<CircuitHandler, CircuitHandlerProxy>();

            // -- Module � Tableaux de Bord RH � (�tape 2 + 4.x) -----------
            // Cache m�moire utilis� par les services Dashboards pour
            // m�moriser les KPI lourds (TTL configur� c�t� service, 5 min
            // par d�faut). Les services concrets sont ajout�s au fur
            // et � mesure des Tableaux 1 ? 6 (�tape 4).
            services.AddMemoryCache();

            // Tableau N�1 � Effectif d�taill� (�tape 4.1)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IEffectifDetailleDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.EffectifDetailleDashboardService>();

            // Tableau N�2 � Analyse de l'Effectif (�tape 4.2)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IAnalyseEffectifDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.AnalyseEffectifDashboardService>();

            // Tableau N�3 � Mouvements (Arriv�es / D�parts) (�tape 4.3)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IMouvementsDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.MouvementsDashboardService>();

            // Tableau N�4 � R�mun�ration (�galit� des salaires) (�tape 4.4)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IRemunerationDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.RemunerationDashboardService>();

            // Tableau N�5 � Suivi des Absences (�tape 4.5)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.ISuiviAbsencesDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.SuiviAbsencesDashboardService>();

            // Tableau N�6 � Bilan Social Mensuel (�tape 4.6)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IBilanSocialDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.BilanSocialDashboardService>();

            // --- V1.2 � Pilotage strat�gique DAF + DRH ---------------------
            // Tableau N�7 � Budget vs R�alis� Masse Salariale (Sprint 2)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IBudgetVsRealiseDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.BudgetVsRealiseDashboardService>();

            // Tableau N�8 � Provisions Sociales (IDR + Cong�s pay�s) (Sprint 3)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IProvisionsSocialesDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.ProvisionsSocialesDashboardService>();

            // Tableau N�9 � Co�t Complet par Salari� (Fully Loaded Cost) (Sprint 4)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.ICoutCompletDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.CoutCompletDashboardService>();

            // Tableau N�10 � Conformit� S�n�gal (audit-ready) (Sprint 5)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IConformiteSenegalDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.ConformiteSenegalDashboardService>();

            // --- V1.3 Sprint 1 � Co�t R�el Int�rimaires -------------------
            // Service d'import du livre de paie int�rim (parsing xlsx + matching)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Interim.IBulletinInterimImportService,
                AdiPAIE_V02.Module.Services.Interim.BulletinInterimImportService>();

            // Tableau N�11 � Co�t R�el Int�rimaires (Dashboard)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.ICoutReelInterimDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.CoutReelInterimDashboardService>();

            // Tableau N�12 � Pilotage Recrutement (Dashboard V1.4)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IPilotageRecrutementDashboardService,
                AdiPAIE_V02.Module.Services.Dashboards.PilotageRecrutementDashboardService>();

            // Export Excel partag� pour les 6 tableaux (�tape 7.2)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IDashboardExcelExportService,
                AdiPAIE_V02.Module.Services.Dashboards.DashboardExcelExportService>();

            // Export PDF partag� pour les 6 tableaux (�tape 7.3 � QuestPDF)
            services.AddScoped<
                AdiPAIE_V02.Module.Services.Dashboards.IDashboardPdfExportService,
                AdiPAIE_V02.Module.Services.Dashboards.DashboardPdfExportService>();

            // V1.4 � Hosted services d�sactiv�s temporairement (debug deploy IIS Production)
            // Cause un NullReferenceException dans DxResourceManager.RegisterTheme au prerendering
            // � r�activer apr�s fix DevExpress (ticket support en cours)
            services.AddHostedService<AttestationRappelService>();
            services.AddHostedService<DossierExpirationRappelService>();
            services.AddHostedService<EvaluationFroidRappelService>();
            services.AddHostedService<AlerteInterimaireService>();
            // V1.5.2 (QW5) — Cron quotidien 06:00 : alerte fin de mission intérim ≤ 30j
            services.AddHostedService<Services.AlerteFinMissionInterimHostedService>();

            services.AddXaf(Configuration, builder =>
            {
                builder.UseApplication<AdiPAIE_V02BlazorApplication>();
                builder.Modules
                    .AddAuditTrailXpo()
                    .AddCloning()
                    .AddConditionalAppearance()
                    .AddDashboards(options =>
                    {
                        options.DashboardDataType = typeof(DevExpress.Persistent.BaseImpl.DashboardData);
                    })
                    .AddFileAttachments()
                    .AddNotifications()
                    .AddOffice()
                    .AddReports(options =>
                    {
                        options.EnableInplaceReports = true;
                        options.ReportDataType = typeof(DevExpress.Persistent.BaseImpl.ReportDataV2);
                        options.ReportStoreMode = DevExpress.ExpressApp.ReportsV2.ReportStoreModes.XML;
                    })
                    .AddScheduler()
                    .AddStateMachine(options =>
                    {
                        options.StateMachineStorageType = typeof(DevExpress.ExpressApp.StateMachine.Xpo.XpoStateMachine);
                    })
                    .AddValidation(options =>
                    {
                        options.AllowValidationDetailsAccess = false;
                    })
                    .AddViewVariants()
                    .Add<AdiPAIE_V02.Module.AdiPAIE_V02Module>()
                    .Add<AdiPAIE_V02BlazorModule>();
//                builder.AddMultiTenancy()
//                    .WithHostDatabaseConnectionString(Configuration.GetConnectionString("ConnectionString"))
//#if EASYTEST
//                    .WithHostDatabaseConnectionString(Configuration.GetConnectionString("EasyTestConnectionString"))
//#endif
//                    .WithMultiTenancyModelDifferenceStore(options =>
//                    {
//#if !RELEASE
//                        options.UseTenantSpecificModel = false;
//#endif
//                    })
//                    .WithTenantResolver<TenantByEmailResolver>();

                builder.ObjectSpaceProviders
                    .AddSecuredXpo((serviceProvider, options) =>
                    {
                        string connectionString = serviceProvider
       .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
       .GetConnectionString("ConnectionString");

                        // V1.1 � Si le password est chiffr� (DPAPI:base64...), le d�chiffrer
                        // pour SqlClient. Transparent si pas de chiffrement (legacy).
                        connectionString = AdiPAIE_V02.Module.Services.DbConfigHelper
                            .DecryptConnectionString(connectionString);

                        // Ajouter TrustServerCertificate=True si absent
                        // (�vite l'erreur SSL avec SQL Server Express / certificat auto-sign�)
                        if (!string.IsNullOrWhiteSpace(connectionString)
                            && !connectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
                        {
                            connectionString = connectionString.TrimEnd(';') + ";TrustServerCertificate=True";
                        }

                        options.ConnectionString = connectionString;
                        options.ThreadSafe = true;
                        options.UseSharedDataStoreProvider = true;
                    })
                    .AddNonPersistent();
                builder.Security
                    .UseIntegratedMode(options =>
                    {
                        options.Lockout.Enabled = true;

                        options.RoleType = typeof(PermissionPolicyRole);
                        // ApplicationUser descends from PermissionPolicyUser and supports the OAuth authentication. For more information, refer to the following topic: https://docs.devexpress.com/eXpressAppFramework/402197
                        // If your application uses PermissionPolicyUser or a custom user type, set the UserType property as follows:
                        options.UserType = typeof(AdiPAIE_V02.Module.BusinessObjects.ApplicationUser);
                        // ApplicationUserLoginInfo is only necessary for applications that use the ApplicationUser user type.
                        // If you use PermissionPolicyUser or a custom user type, comment out the following line:
                        options.UserLoginInfoType = typeof(AdiPAIE_V02.Module.BusinessObjects.ApplicationUserLoginInfo);
                        options.UseXpoPermissionsCaching();
                        options.Events.OnSecurityStrategyCreated += securityStrategy =>
                        {
                            // Use the 'PermissionsReloadMode.NoCache' option to load the most recent permissions from the database once
                            // for every Session instance when secured data is accessed through this instance for the first time.
                            // Use the 'PermissionsReloadMode.CacheOnFirstAccess' option to reduce the number of database queries.
                            // In this case, permission requests are loaded and cached when secured data is accessed for the first time
                            // and used until the current user logs out.
                            // See the following article for more details: https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Security.SecurityStrategy.PermissionsReloadMode.
                            ((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
                            SecurityStrategy.EnableSecurityForActions = true;
                        };
                    })
                    .AddPasswordAuthentication(options =>
                    {
                        options.IsSupportChangePassword = true;
                    });
            });
            var authentication = services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
            authentication.AddCookie(options =>
            {
                options.LoginPath = "/LoginPage";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. To change this for production scenarios, see: https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseRequestLocalization();
            app.UseStaticFiles();
            app.UseRouting();
          
            // V1.4.3 — Culture fr-FR avec override FCFA (XOF) pour le Sénégal.
            // Évite l'apparition du "€" partout où XAF rend une decimal en mode
            // currency (Bulletin.NetAPayer, etc.) sans DisplayFormat explicite.
            var cultureInfo = (System.Globalization.CultureInfo)
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR").Clone();
            cultureInfo.NumberFormat.CurrencySymbol = "FCFA";
            cultureInfo.NumberFormat.CurrencyDecimalDigits = 0; // FCFA n'a pas de centimes
            cultureInfo.NumberFormat.CurrencyPositivePattern = 3; // "1 234 FCFA"
            cultureInfo.NumberFormat.CurrencyNegativePattern = 8; // "-1 234 FCFA"

            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultureInfo),
                SupportedCultures = new[] { cultureInfo },
                SupportedUICultures = new[] { cultureInfo }
            });

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.UseXaf();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapXafEndpoints();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapControllers();
            });
        }
    }
}

using JobPolice.Classes;
using JobPolice.Entities;
using JobPolice.Points;
using Life;
using Life.BizSystem;
using Life.DB;
using Life.Network;
using Life.UI;
using Life.VehicleSystem;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using ModKit.Interfaces;
using ModKit.Internal;
using ModKit.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace JobPolice
{
    public class JobPolice : ModKit.ModKit
    {
        public static string ConfigDirectoryPath;
        public static string ConfigJobPoliceConfigFilePath;
        private readonly MyEvents _events;
        public static JobPoliceConfig _jobPoliceConfig;

        public JobPolice(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
            _events = new MyEvents(api);
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitDirectoryAndFiles();
            InitEntitiesAndPoints();
            InsertMenu();

            _events.Init(Nova.server);
            _jobPoliceConfig = LoadConfigFile(ConfigJobPoliceConfigFilePath);

            new SChatCommand("/jobpolice", "mise à jour avec le fichier config jobpolice", "/jobpolice", (player, arg) => {
                if(player.IsAdmin)
                {
                    _jobPoliceConfig = LoadConfigFile(ConfigJobPoliceConfigFilePath);
                    player.Notify("JobPolice", "Config mise à jour", NotificationManager.Type.Success);
                }
            }).Register();

            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        #region CONFIG
        private void InitDirectoryAndFiles()
        {
            try
            {
                ConfigDirectoryPath = DirectoryPath + "/JobPolice";
                ConfigJobPoliceConfigFilePath = Path.Combine(ConfigDirectoryPath, "Config.json");

                if (!Directory.Exists(ConfigDirectoryPath)) Directory.CreateDirectory(ConfigDirectoryPath);
                if (!File.Exists(ConfigJobPoliceConfigFilePath)) InitDrugsConfigFile();
            }
            catch (IOException ex)
            {
                Logger.LogError("InitDirectory", ex.Message);
            }
        }

        private void InitDrugsConfigFile()
        {
            JobPoliceConfig jobPoliceConfig = new JobPoliceConfig();

            jobPoliceConfig.DurationOfAlcohol = 20;
            jobPoliceConfig.DurationOfCannabis = 20;
            jobPoliceConfig.DurationOfDruged = 20;
            jobPoliceConfig.LawEnforcementBizId = 1;

            string json = JsonConvert.SerializeObject(jobPoliceConfig, Formatting.Indented);
            File.WriteAllText(ConfigJobPoliceConfigFilePath, json);
        }

        private JobPoliceConfig LoadConfigFile(string path)
        {
            if (File.Exists(path))
            {
                string jsonContent = File.ReadAllText(path);
                JobPoliceConfig drugsConfig = JsonConvert.DeserializeObject<JobPoliceConfig>(jsonContent);

                return drugsConfig;
            }
            else return null;
        }
        #endregion

        public void InitEntitiesAndPoints()
        {
            Orm.RegisterTable<JobPoliceVehicle>();
            Orm.RegisterTable<JobPoliceCitizen>();
            Orm.RegisterTable<JobPoliceOffense>();
            Orm.RegisterTable<JobPoliceRecord>();
            Orm.RegisterTable<JobPoliceDrugs>();

            Orm.RegisterTable<JobPoliceCentralPoint>();
            PointHelper.AddPattern(nameof(JobPoliceCentralPoint), new JobPoliceCentralPoint(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, nameof(JobPoliceCentralPoint), new JobPoliceCentralPoint(false), this);

            Orm.RegisterTable<JobPoliceSanctionPoint>();
            PointHelper.AddPattern(nameof(JobPoliceSanctionPoint), new JobPoliceSanctionPoint(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, nameof(JobPoliceSanctionPoint), new JobPoliceSanctionPoint(false), this);
        }

        public void InsertMenu()
        {
            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Dépistage d'alcoolémie", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    var query = await JobPoliceDrugs.Query(d => d.CharacterId == target.character.Id);
                    if (query != null && query.Count > 0)
                    {
                        var isPositive = DateUtils.IsGreater(query[0].LastAlcohol, _jobPoliceConfig.DurationOfAlcohol);
                        player.SendText($"{mk.Color("Contrôle d'alcoolémie:", mk.Colors.Info)} {target.GetFullName()} est {(isPositive ? mk.Color("NÉGATIF", mk.Colors.Success) : mk.Color("POSITIF", mk.Colors.Error))} à l'alcool");
                    } else player.SendText($"{mk.Color("Contrôle d'alcoolémie:", mk.Colors.Info)} {target.GetFullName()} est {mk.Color("NÉGATIF", mk.Colors.Success)} à l'alcool");
                }
                else player.Notify("Contrôle d'alcoolémie", "Aucun citoyen n'est à votre proximité", NotificationManager.Type.Error);
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Dépistage du THC", async (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    var query = await JobPoliceDrugs.Query(d => d.CharacterId == target.character.Id);
                    if (query != null && query.Count > 0)
                    {
                        var isPositive = DateUtils.IsGreater(query[0].LastCannabis, _jobPoliceConfig.DurationOfCannabis);
                        player.SendText($"{mk.Color("Dépistage du THC:", mk.Colors.Info)} {target.GetFullName()} est {(isPositive ? mk.Color("NÉGATIF", mk.Colors.Success) : mk.Color("POSITIF", mk.Colors.Error))} au THC");
                    }
                    else player.SendText($"{mk.Color("Dépistage du THC:", mk.Colors.Info)} {target.GetFullName()} est {mk.Color("NÉGATIF", mk.Colors.Success)} au THC");
                }
                else player.Notify("Dépistage du THC", "Aucun citoyen n'est à votre proximité", NotificationManager.Type.Error);
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Verbaliser", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    List<int> offenseList = new List<int>();
                    JobPoliceVerbalize(player, target, offenseList);
                }
                else player.Notify("Verbaliser", "Aucun citoyen n'est à votre proximité", NotificationManager.Type.Error);
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Fouiller", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    if (target.Health <= 0)
                    {
                        if (InventoryUtils.TargetOpenPlayerInventory(player, target)) target.Notify("Fouille policière", "Le policier fouille votre corps inanimé.", NotificationManager.Type.Warning, 6);
                        else player.Notify("Échec", "Nous n'avons pas pu accéder à l'inventaire de votre cible", NotificationManager.Type.Error);
                    }
                    else
                    {
                        player.Notify("Fouille policière", "Vous demandez au citoyen de le fouiller", NotificationManager.Type.Info);
                        JobPoliceToBeFrisked(target, player);
                    }
                }
                else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Menotter", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);

                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    target.setup.NetworkisRestrain = !target.setup.NetworkisRestrain;
                    if(target.setup.NetworkisRestrain) target.Notify("Menotté !", "Un policier vient de vous passer les menottes", NotificationManager.Type.Warning, 10);
                    else target.Notify("Démenotté", "Vous venez d'être démenotté", NotificationManager.Type.Warning, 10);

                }
                else player.Notify("Échec", "Aucun citoyen à proximité", NotificationManager.Type.Error);
            });

            /*_menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Forcer à suivre", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //choper le joueur menotté à proximité
                //l'emmener de force devant le joueur avec impossibilité de s'en défaire
            });*/

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, $"{mk.Color("Contacter Centrale", mk.Colors.Info)}", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                CentralPanel(player);
            });



            /*_menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Enquêtes", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //différentes pages + état de l'enquête
            });*/

            /*_menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Casier judiciaire", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //rattaché à un citoyen (playerId) + fichier S + bracelet electronique (bip lorsqu'il pénètre dans certains lieux)
            });*/

            //avis de recherche
            //système de plainte avec date
            //système calcule auto de la peine + possibilité de réduction
            //montrer sa plaque
        }

        #region PANEL
        public void CentralPanel(Player player)
        {
            Panel panel = PanelHelper.Create("Centrale", UIPanel.PanelType.Tab, player, () => CentralPanel(player));

            
            panel.AddTabLine("Contrôler un véhicule", async ui => {
                var vehicle = player.GetClosestVehicle();

                if (vehicle != null)
                {
                    LifeVehicle vehicleDb = Nova.v.GetVehicle(vehicle.plate);

                    if (vehicle != null)
                    {
                        var owner = await LifeDB.FetchCharacter(vehicleDb.permissions.owner.characterId);
                        if(owner != null)
                        {
                            player.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le véhicule immatriculé {vehicle.plate} appartient à {owner.Firstname} {owner.Lastname}");
                        }
                        else player.Notify("Erreur", "Véhicule introuvable en base de données", NotificationManager.Type.Error);
                    }
                    else player.Notify("Erreur", "Véhicule introuvable en base de données", NotificationManager.Type.Error);

                    var query = await JobPoliceVehicle.Query(v => v.Plate == vehicle.plate);
                    await Task.Delay(2000);
                    if (query != null && query.Count != 0) player.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le véhicule immatriculé {vehicle.plate} est recherché pour le motif suivant: {query[0].Reason}");
                    else player.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le véhicule immatriculé {vehicle.plate} ne figure pas dans vos bases de données.");
                }
                else player.Notify("Contrôler un véhicule", "Aucun véhicule à proximité", NotificationManager.Type.Error);
            });
            panel.AddTabLine("Contrôler un citoyen", ui => {
                var target = player.GetClosestPlayer();

                if (target != null)
                {
                    player.Notify("Contrôler un citoyen", "Vous avez demandé la carte d'identité du citoyen à proximité", NotificationManager.Type.Success);
                    JobPoliceAskCI(target, player);
                }
                else player.Notify("Contrôler un citoyen", "Aucun citoyen à proximité", NotificationManager.Type.Error);
            });
            //panel.AddTabLine("Policiers en ville", _ => {});
            if (player.character.Id == player.biz.OwnerId) panel.AddTabLine($"{mk.Color("Installer les points", mk.Colors.Warning)}", ui => JobPolicePointsPanel(player));

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.AddButton("Retour", ui => {
                AAMenu.AAMenu.menu.BizPanel(player, AAMenu.AAMenu.menu.BizTabLines);
            });
            panel.CloseButton();

            panel.Display();
        }

        public void JobPoliceAskCI(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Contrôle d'identité", UIPanel.PanelType.Text, toPlayer, () => JobPoliceAskCI(toPlayer, fromPlayer));

            panel.TextLines.Add($"Un agent des forces de l'ordre souhaite contrôler votre identité.");

            panel.CloseButtonWithAction("Accepter", async () =>
            {
                toPlayer.Notify("Contrôle d'identité", "Vous présentez votre carte d'identité", NotificationManager.Type.Warning, 6);

                Character characterJson = toPlayer.GetCharacterJson();
                fromPlayer.setup.TargetCreateCNI(characterJson);

                var query = await JobPoliceCitizen.Query(c => c.Firstname == toPlayer.character.Firstname && c.Lastname == toPlayer.character.Lastname || c.CharacterId == toPlayer.character.Id);
                if (query != null && query.Count != 0)
                {
                    fromPlayer.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le citoyen \"{toPlayer.GetFullName()}\" est connu de nos services.");
                    if (query[0].IsDangerous) fromPlayer.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} {mk.Color("ATTENTION", mk.Colors.Warning)} \"{toPlayer.GetFullName()}\" est {mk.Color("FICHIER S", mk.Colors.Warning)}");
                    if (query[0].IsWanted)
                    {
                        fromPlayer.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} {mk.Color("ALERTE", mk.Colors.Error)} \"{toPlayer.GetFullName()}\" est {mk.Color("RECHERCHÉ", mk.Colors.Error)}");
                        fromPlayer.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} \"{toPlayer.GetFullName()}\" est recherché pour {(query[0].Reason?.Length > 0 ? query[0].Reason : "???")}");
                    }
                }
                else fromPlayer.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le citoyen est inconnu de nos services");

                return await Task.FromResult(true);
            });
            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Contrôle d'identité", "Le citoyen refuse de montrer sa carte d'identité", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Contrôle d'identité", "Vous refusez de présenter votre carte d'identité", NotificationManager.Type.Warning, 6);
                return await Task.FromResult(true);
            });

            panel.Display();
        }

        public async void JobPolicePointsPanel(Player player)
        {
            var points = await NPoint.Query(p => p.TypeName == nameof(JobPoliceCentralPoint) || p.TypeName == nameof(JobPoliceSanctionPoint));

            #region JobPoliceCentral
            var centralPoints = points.Where(p => p.TypeName == nameof(JobPoliceCentralPoint)).ToList();
            var centralPatterns = await JobPoliceCentralPoint.Query(p => p.BizId == player.biz.Id);
            List<int> centralPatternIds = centralPatterns.Select(p => p.Id).ToList();
            var centralPointsResult = centralPoints.Where(point => centralPatternIds.Contains(point.PatternId)).ToList();
            #endregion

            #region JobPoliceSanction
            var sanctionPoints = points.Where(p => p.TypeName == nameof(JobPoliceSanctionPoint)).ToList();
            var sanctionPatterns = await JobPoliceSanctionPoint.Query(p => p.BizId == player.biz.Id);
            List<int> sanctionPatternIds = sanctionPatterns.Select(p => p.Id).ToList();
            var sanctionPointsResult = sanctionPoints.Where(point => sanctionPatternIds.Contains(point.PatternId)).ToList();
            #endregion

            Panel panel = PanelHelper.Create("Points des forces de l'ordre", UIPanel.PanelType.Tab, player, () => JobPolicePointsPanel(player));

            panel.AddTabLine("Point du centre de commandement", async ui => {
                if (centralPointsResult?.Count > 0)
                {
                    bool result = await PointHelper.SetNPointPosition(player, centralPointsResult[0]);
                    if (result) panel.Refresh();
                    else player.Notify("Oops", "Votre point est déjà sur cette position", NotificationManager.Type.Error);
                }
                else
                {
                    JobPoliceCentralPoint centralPoint = new JobPoliceCentralPoint(false);
                    centralPoint.PatternName = $"Centrale";
                    centralPoint.BizId = player.biz.Id;
                    await centralPoint.Save();
                    await PointHelper.CreateNPoint(player, centralPoint);
                    panel.Refresh();
                }
            });
            panel.AddTabLine("Point d'application des peines", async ui =>
            {
                if (sanctionPointsResult?.Count > 0)
                {
                    bool result = await PointHelper.SetNPointPosition(player, sanctionPointsResult[0]);
                    if (result) panel.Refresh();
                    else player.Notify("Oops", "Votre point est déjà sur cette position", NotificationManager.Type.Error);
                }
                else
                {
                    JobPoliceSanctionPoint sanctionPoint = new JobPoliceSanctionPoint(false);
                    sanctionPoint.PatternName = $"Sanction";
                    sanctionPoint.BizId = player.biz.Id;
                    await sanctionPoint.Save();
                    await PointHelper.CreateNPoint(player, sanctionPoint);
                    panel.Refresh();
                }
            });

            panel.AddButton("Installer", ui => panel.SelectTab());
            panel.AddButton("Retirer", async ui =>
            {
                switch (panel.selectedTab)
                {
                    case 0:
                        await PointHelper.DeleteNPointsByPattern(player, centralPatterns[0]);
                        await centralPatterns[0].Delete();
                        break;
                    case 1:
                        await PointHelper.DeleteNPointsByPattern(player, sanctionPatterns[0]);
                        await sanctionPatterns[0].Delete();
                        break;
                    default:
                        ModKit.Internal.Logger.LogError("JobPolicePointsPanel", "Erreur lors de la sélection du point.");
                        break;
                }
                panel.Refresh();
            });
            panel.AddButton("Retour", ui => {
                AAMenu.AAMenu.menu.BizPanel(player, AAMenu.AAMenu.menu.BizTabLines);
            });
            panel.CloseButton();

            panel.Display();
        }

        public void JobPoliceToBeFrisked(Player toPlayer, Player fromPlayer)
        {
            Panel panel = PanelHelper.Create($"Fouille policière", UIPanel.PanelType.Text, toPlayer, () => JobPoliceToBeFrisked(toPlayer, fromPlayer));

            panel.TextLines.Add($"Une policier souhaite procéder à une fouille.");

            panel.CloseButtonWithAction("Coopérer", async () =>
            {
                if (InventoryUtils.TargetOpenPlayerInventory(fromPlayer, toPlayer))
                {
                    fromPlayer.Notify("Fouille policière", "Ce citoyen coopère et vous débutez votre fouille", NotificationManager.Type.Warning, 6);
                    fromPlayer.SendText($"{mk.Color("Fouille policière:", mk.Colors.Info)} {toPlayer.GetFullName()} possède {mk.Color($"{toPlayer.character.Money}", mk.Colors.Warning)}€ en liquide.");
                    toPlayer.Notify("Fouille policière", "Vous avez accepté d'être fouillé", NotificationManager.Type.Warning, 6);
                    return await Task.FromResult(true);
                }
                else
                {
                    fromPlayer.Notify("Erreur", "Nous n'avons pas pu accéder à l'inventaire du joueur ciblé", NotificationManager.Type.Error);
                    toPlayer.Notify("Erreur", "Le joueur à proximité n'a pas pu accéder à votre inventaire", NotificationManager.Type.Error);
                    return await Task.FromResult(true);
                }
            });
            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Fouille policière", "Ce citoyen refuse de coopérer", NotificationManager.Type.Warning, 6);
                toPlayer.Notify("Fouille policière", "Vous avez refusé d'être fouillé", NotificationManager.Type.Warning, 6);
                return await Task.FromResult(true);
            });

            panel.Display();
        }

        public async void JobPoliceVerbalize(Player fromPlayer, Player toPlayer, List<int> offenseList)
        {
            var query = await JobPoliceOffense.Query(q => q.PrisonTime < 1 && q.OffenseType != null);
            double money = 0.0;
            int points = 0;

            Panel panel = PanelHelper.Create($"Verbaliser", UIPanel.PanelType.TabPrice, fromPlayer, () => JobPoliceVerbalize(fromPlayer, toPlayer, offenseList));

            if (query != null && query.Count > 0)
            {
                foreach (var offense in query)
                {
                    var isSelected = offenseList.Contains(offense.Id);
                    if (isSelected)
                    {
                        money += offense.Money;
                        points += offense.Points;
                    }
                    panel.AddTabLine($"{mk.Color($"{(offense.Title != null ? $"{offense.Title}" : "à définir")}", isSelected ? mk.Colors.Success : mk.Colors.Error)}", $"{(offense.OffenseType != null ? $"{offense.OffenseType}" : "à définir")}", offense.OffenseType != null ? ItemUtils.GetIconIdByItemId(offense.OffenseTag[offense.OffenseType]) : -1, ui =>
                    {
                        if (isSelected) offenseList.Remove(offense.Id);
                        else offenseList.Add(offense.Id);
                        JobPoliceVerbalize(fromPlayer, toPlayer, offenseList);
                    });
                }
                panel.AddTabLine($"{mk.Color("Montant de l'amende", mk.Colors.Info)}", $"{money}€", -1, _ => { });
                panel.AddTabLine($"{mk.Color("Retrait points de permis B", mk.Colors.Info)}", $"{points} points", -1, _ => { });

                panel.NextButton("Ajouter", () => panel.SelectTab());
                panel.CloseButtonWithAction("Verbaliser", async () =>
                {
                    JobPoliceVerbalizeRequest(toPlayer, fromPlayer, money, points);
                    return await Task.FromResult(true);
                });
            }
            else panel.AddTabLine("Aucune infractions enregistrée", _ => { });

            panel.CloseButton();

            panel.Display();
        }

        public void JobPoliceVerbalizeRequest(Player fromPlayer, Player toPlayer, double money, int points)
        {
            Panel panel = PanelHelper.Create($"Verbaliser", UIPanel.PanelType.Text, fromPlayer, () => JobPoliceVerbalizeRequest(fromPlayer, toPlayer, money, points));

            panel.TextLines.Add($"{mk.Color("Montant de l'amende: ", mk.Colors.Info)} {money}€");
            panel.TextLines.Add($"{mk.Color("Retrait points de permis: ", mk.Colors.Info)} {points}");

            panel.CloseButtonWithAction("Accepter", async () =>
            {
                toPlayer.Notify("Verbalisation", $"Le citoyen accepte la verbalisation", NotificationManager.Type.Success);

                if (fromPlayer.character.Bank >= money)
                {
                    fromPlayer.AddBankMoney(-money);
                    toPlayer.biz.Bank += money;
                    toPlayer.Notify("Verbalisation", $"Le citoyen paye son amende", NotificationManager.Type.Success);
                    fromPlayer.Notify("Verbalisation", $"Vous venez de payer {money}€ d'amende depuis votre compte en banque", NotificationManager.Type.Info, 10);
                }
                else if (fromPlayer.character.Money >= money)
                {
                    fromPlayer.AddMoney(-money, "verbalisation");
                    toPlayer.biz.Bank += money;
                    toPlayer.Notify("Verbalisation", $"Le citoyen paye son amende", NotificationManager.Type.Success);
                    fromPlayer.Notify("Verbalisation", $"Vous venez de payer {money}€ d'amende en liquide", NotificationManager.Type.Info, 10);
                } else
                {
                    fromPlayer.Notify("Verbalisation", $"Vous n'avez pas les moyens de payer {money}€ d'amende.", NotificationManager.Type.Error, 10);
                    toPlayer.Notify("Verbalisation", $"Le citoyen n'a pas les moyens de payer l'amende", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }

                if (!fromPlayer.character.PermisB)
                {
                    toPlayer.Notify("Verbalisation", $"Le citoyen n'ayant pas le permis B, aucun points ne peut être retiré.", NotificationManager.Type.Success);
                } else
                {
                    fromPlayer.character.PermisPoints -= points;
                    if (fromPlayer.character.PermisPoints <= 0)
                    {
                        fromPlayer.character.PermisPoints = 0;
                        fromPlayer.character.PermisB = false;
                    }

                    if (fromPlayer.character.PermisB)
                    {
                        toPlayer.Notify("Verbalisation", $"Vous venez de retirer {points}points sur le permis de {toPlayer.GetFullName()}", NotificationManager.Type.Success);
                        fromPlayer.Notify("Verbalisation", $"Vous venez de perdre {points} sur votre permis B.<br>Vous n'avez plus que {fromPlayer.character.PermisPoints} points", NotificationManager.Type.Info, 10);
                    }
                    else
                    {
                        toPlayer.Notify("Verbalisation", $"Vous venez de retirer {points}points sur le permis de {toPlayer.GetFullName()}<br> Il n'a plus de points et perd le permis B.", NotificationManager.Type.Success);
                        fromPlayer.Notify("Verbalisation", $"Vous venez de perdre votre permis B", NotificationManager.Type.Error, 10);
                    }
                }

                return await Task.FromResult(true);
            });
            panel.CloseButtonWithAction("Refuser", async () =>
            {
                fromPlayer.Notify("Verbalisation", $"Vous refusez cette verbalisation", NotificationManager.Type.Warning);
                toPlayer.Notify("Verbalisation", $"Le citoyen refuse la verbalisation", NotificationManager.Type.Error);
                return await Task.FromResult(true);
            });

            panel.Display();
        }
        #endregion
    }
}

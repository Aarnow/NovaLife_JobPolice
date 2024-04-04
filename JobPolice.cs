using JobPolice.Entities;
using Life;
using Life.BizSystem;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using ModKit.Interfaces;
using ModKit.Internal;
using System.Collections.Generic;
using System.Linq;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace JobPolice
{
    public class JobPolice : ModKit.ModKit
    {
        public int centralId { get; set; }

        public JobPolice(IGameAPI api) : base(api)
        {
            centralId = 1;
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitEntitiesAndPoints();
            InsertMenu();

            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public void InitEntitiesAndPoints()
        {
            Orm.RegisterTable<JobPoliceVehicle>();
            Orm.RegisterTable<JobPoliceCitizen>();
            Orm.RegisterTable<JobPoliceOffense>();
            Orm.RegisterTable<JobPoliceRecord>();

            Orm.RegisterTable<JobPoliceCentralPoint>();
            PointHelper.AddPattern(nameof(JobPoliceCentralPoint), new JobPoliceCentralPoint(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, nameof(JobPoliceCentralPoint), new JobPoliceCentralPoint(false), this);
        }

        public void InsertMenu()
        {
            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Dépistage d'alcoolémie", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //degré d'alcool dans le sang
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Dépistage du THC", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //positif négatif
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Verbaliser", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //dossier
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Fouiller", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //permet d'ouvrir l'inventaire du citoyen + informer le policier de la somme d'argent liquide qu'ils transportent
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Retirer points permis", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //dossier
            });

            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, "Mettre en prison", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                //dossier
            });


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
                    var query = await JobPoliceVehicle.Query(v => v.Plate == vehicle.plate);
                    if (query != null && query.Count != 0) player.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le véhicule immatriculé {vehicle.plate} est recherché pour {query[0].Reason}");
                    else player.SendText($"{mk.Color("Centrale:", mk.Colors.Info)} le véhicule immatriculé {vehicle.plate} ne figure pas dans vos bases de données.");
                }
                else player.Notify("Contrôler un véhicule", "Aucun véhicule à proximité", NotificationManager.Type.Error);
            });
            panel.AddTabLine("Contrôler un citoyen", _ => {});
            //panel.AddTabLine("Policiers en ville", _ => {});
            if (player.character.Id == player.biz.OwnerId) panel.AddTabLine($"{mk.Color("Installer les points", mk.Colors.Warning)}", ui => JobPolicePointsPanel(player));

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.AddButton("Retour", ui => {
                AAMenu.AAMenu.menu.BizPanel(player, AAMenu.AAMenu.menu.BizTabLines);
            });
            panel.CloseButton();

            panel.Display();
        }

        public async void JobPolicePointsPanel(Player player)
        {
            var points = await NPoint.Query(p => p.TypeName == nameof(JobPoliceCentralPoint));

            #region JobPoliceCentral
            var centralPoints = points.Where(p => p.TypeName == nameof(JobPoliceCentralPoint)).ToList();
            var centralPatterns = await JobPoliceCentralPoint.Query(p => p.BizId == player.biz.Id);
            List<int> centralPatternIds = centralPatterns.Select(p => p.Id).ToList();
            var centralPointsResult = centralPoints.Where(point => centralPatternIds.Contains(point.PatternId)).ToList();
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

            panel.AddButton("Installer", ui => panel.SelectTab());
            panel.AddButton("Retirer", async ui =>
            {
                switch (panel.selectedTab)
                {
                    case 0:
                        await PointHelper.DeleteNPointsByPattern(player, centralPatterns[0]);
                        await centralPatterns[0].Delete();
                        break;
                    default:
                        Logger.LogError("JobPolicePointsPanel", "Erreur lors de la sélection du point.");
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
        #endregion
    }
}

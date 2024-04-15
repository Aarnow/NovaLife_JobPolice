using Life.Network;
using Life.UI;
using SQLite;
using System.Threading.Tasks;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using mk = ModKit.Helper.TextFormattingHelper;
using System.Collections.Generic;
using System.Linq;
using System;
using JobPolice.Entities;
using ModKit.Utils;
using static UnityEngine.GraphicsBuffer;
using Steamworks.Ugc;

namespace JobPolice.Points
{
    public class JobPoliceSanctionPoint : ModKit.ORM.ModEntity<JobPoliceSanctionPoint>, PatternData
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string TypeName { get; set; }
        public string PatternName { get; set; }

        //Declare your other properties here
        public int BizId { get; set; }

        [Ignore] public ModKit.ModKit Context { get; set; }

        public JobPoliceSanctionPoint() { }
        public JobPoliceSanctionPoint(bool isCreated)
        {
            TypeName = nameof(JobPoliceSanctionPoint);
        }

        /// <summary>
        /// Applies the properties retrieved from the database during the generation of a point in the game using this model.
        /// </summary>
        /// <param name="patternId">The identifier of the pattern in the database.</param>
        public async Task SetProperties(int patternId)
        {
            var result = await Query(patternId);

            Id = patternId;
            TypeName = nameof(JobPoliceSanctionPoint);
            PatternName = result.PatternName;

            //Add your other properties here
            BizId = result.BizId;
        }

        /// <summary>
        /// Contains the action to perform when a player interacts with the point.
        /// </summary>
        /// <param name="player">The player interacting with the point.</param>
        public async void OnPlayerTrigger(Player player)
        {
            if (player.biz != null && player.biz.IsActivity(Life.BizSystem.Activity.Type.LawEnforcement)) JobPoliceSanctionPanel(player);
            else {
                var citizen = await JobPoliceCitizen.Query(c => c.CharacterId == player.character.Id);
                if (citizen != null && citizen.Count > 0) JobPoliceCitizenSanctionPanel(player, citizen[0]);
                else player.Notify("Casier judiciaire", "Vous n'avez aucun casier", Life.NotificationManager.Type.Info);
            }
           
        }

        /// <summary>
        /// Triggers the function to begin creating a new model.
        /// </summary>
        /// <param name="player">The player initiating the creation of the new model.</param>
        public void SetPatternData(Player player)
        {
            //Set the function to be called when a player clicks on the “create new model” button
            SetName(player);
        }

        /// <summary>
        /// Displays all properties of the pattern specified as parameter.
        /// The user can select one of the properties to make modifications.
        /// </summary>
        /// <param name="player">The player requesting to edit the pattern.</param>
        /// <param name="patternId">The ID of the pattern to be edited.</param>
        public async void EditPattern(Player player, int patternId)
        {
            JobPoliceSanctionPoint pattern = new JobPoliceSanctionPoint(false);
            pattern.Context = Context;
            await pattern.SetProperties(patternId);

            Panel panel = Context.PanelHelper.Create($"Modifier un {pattern.TypeName}", UIPanel.PanelType.Tab, player, () => EditPattern(player, patternId));


            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {pattern.PatternName}", _ => {
                pattern.SetName(player, true);
            });
            //Add tablines for your other properties here

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Allows the player to set a name for the pattern, either during creation or modification.
        /// </summary>
        /// <param name="player">The player interacting with the panel.</param>
        /// <param name="inEdition">A flag indicating if the pattern is being edited.</param>
        public void SetName(Player player, bool isEditing = false)
        {
            Panel panel = Context.PanelHelper.Create($"{(!isEditing ? "Créer" : "Modifier")} un modèle de {TypeName}", UIPanel.PanelType.Input, player, () =>
            SetName(player));

            panel.TextLines.Add("Donner un nom à votre modèle");
            panel.inputPlaceholder = "3 caractères minimum";

            if (!isEditing)
            {
                panel.NextButton("Suivant", () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        PatternName = panel.inputText;
                        //function to call for the following property
                        // If you want to generate your point
                        // await Save();
                        // ConfirmGeneratePoint(player, this);
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un nom à votre modèle (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        SetName(player);
                    }
                });
            }
            else
            {
                panel.PreviousButtonWithAction("Confirmer", async () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        PatternName = panel.inputText;
                        if (await Save()) return true;
                        else
                        {
                            player.Notify("Erreur", "échec lors de la sauvegarde de vos changements", Life.NotificationManager.Type.Error);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un nom à votre modèle (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        return false;
                    }
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        #region CUSTOM
        public async void JobPoliceCitizenSanctionPanel(Player player, JobPoliceCitizen citizen)
        {
            var sanctions = await JobPoliceRecord.Query(r => r.CitizenId == citizen.Id);
            
            Panel panel = Context.PanelHelper.Create($"Casier judiciaire \"{citizen.Pseudonym}\"", UIPanel.PanelType.TabPrice, player, () => JobPoliceCitizenSanctionPanel(player, citizen));

            if (sanctions != null && sanctions.Count != 0)
            {
                foreach(var sanction in sanctions)
                {
                    panel.AddTabLine($"Bulletin n°{sanction.Id}: {(sanction.IsPaid ? $"{mk.Color("Payé", mk.Colors.Success)}" : $"{mk.Color("Impayé", mk.Colors.Error)}")}", $"{DateUtils.ConvertNumericalDateToString(sanction.CreatedAt)}", 499, _ => {
                        JobPoliceCitizenSanctionDetailsPanel(player, citizen, sanction);
                    });
                }

                panel.NextButton("Consulter", () => panel.SelectTab());
            }

            if (player.biz != null && player.biz.IsActivity(Life.BizSystem.Activity.Type.LawEnforcement))
            {
                panel.NextButton("Ajouter", () => {
                    var record = new JobPoliceRecord();
                    record.CitizenId = citizen.Id;
                    record.IsPaid = false;
                    record.LOffenseList = new List<int>();
                    record.CreatedBy = player.GetFullName();
                    record.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                    JobPoliceCitizenSanctionAddPanel(player, record);
                });
                panel.PreviousButton();
            }

            panel.CloseButton();

            panel.Display();
        }
        public async void JobPoliceCitizenSanctionAddPanel(Player player, JobPoliceRecord record)
        {
            var offenses = await JobPoliceOffense.QueryAll();

            Panel panel = Context.PanelHelper.Create($"Ajouter une peine", UIPanel.PanelType.TabPrice, player, () => JobPoliceCitizenSanctionAddPanel(player, record));

            if (offenses != null && offenses.Count > 0)
            {
                foreach (var offense in offenses)
                {
                    var isSelected = record.LOffenseList.Contains(offense.Id);
                    panel.AddTabLine($"{mk.Color($"{offense.Title}", isSelected ? mk.Colors.Success : mk.Colors.Error)}", $"{offense.OffenseType}", ItemUtils.GetIconIdByItemId(offense.OffenseTag[offense.OffenseType]), ui =>
                    {
                        if (isSelected)record.LOffenseList.Remove(offense.Id);
                        else record.LOffenseList.Add(offense.Id);
                        JobPoliceCitizenSanctionAddPanel(player, record);
                    });
                }
            }
            else panel.AddTabLine("Aucun", _ => { });

            panel.NextButton("Ajouter/Retirer", () => panel.SelectTab());
            panel.CloseButtonWithAction("Enregistrer", async () => {
                record.OffenseList = ListConverter.WriteJson(record.LOffenseList);
               if(await record.Save())
                {
                    player.Notify("Succès", "Bulletin enregistré !", Life.NotificationManager.Type.Success);
                    return await Task.FromResult(true);
                } else
                {
                    player.Notify("Échec", "Nous n'avons pas pu enregistrer ce nouveau bulletin", Life.NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            });
            panel.CloseButton();

            panel.Display();
        }
        public async void JobPoliceCitizenSanctionDetailsPanel(Player player, JobPoliceCitizen jobPoliceCitizen, JobPoliceRecord jobPoliceRecord)
        {
            var offenses = await JobPoliceOffense.QueryAll();
            jobPoliceRecord.LOffenseList = ListConverter.ReadJson(jobPoliceRecord.OffenseList);
            var prisonTime = 0;
            var money = 0.0;
            var points = 0;

            Panel panel = Context.PanelHelper.Create($"Bulletin n°{jobPoliceRecord.Id}", UIPanel.PanelType.TabPrice, player, () => JobPoliceCitizenSanctionDetailsPanel(player, jobPoliceCitizen, jobPoliceRecord));

            if (offenses != null && offenses.Count != 0)
            {
                foreach (var offense in jobPoliceRecord.LOffenseList)
                {
                    var currentOffense = offenses.Where(o => o.Id == offense).FirstOrDefault();
                    prisonTime += currentOffense.PrisonTime;
                    money += currentOffense.Money;
                    points += currentOffense.Points;
                    panel.AddTabLine($"{currentOffense.Title}", $"{currentOffense.OffenseType}", ItemUtils.GetIconIdByItemId(currentOffense.OffenseTag[currentOffense.OffenseType]), _ => { });
                }

                panel.AddTabLine($"{mk.Color("Temps de prison", mk.Colors.Info)}", $"{prisonTime} secondes", -1, _ => { });
                panel.AddTabLine($"{mk.Color("Montant de l'amende", mk.Colors.Info)}", $"{money}€", -1, _ => { });
                panel.AddTabLine($"{mk.Color("Retrait points de permis B", mk.Colors.Info)}", $"{points} points", -1, _ => { });
            }
            else player.Notify("Erreur", "Nous n'avons pas pu récupérer les infractions", Life.NotificationManager.Type.Error);

            if(jobPoliceCitizen.CharacterId == player.character.Id) panel.CloseButtonWithAction("Payer", async () =>
            {
                if (!jobPoliceRecord.IsPaid)
                {
                    jobPoliceRecord.IsPaid = true;
                    if (await jobPoliceRecord.Save())
                    {
                        player.AddBankMoney(-money);
                        player.character.PermisPoints -= points;
                        if (player.character.PermisPoints <= 0)
                        {
                            player.character.PermisPoints = 0;
                            player.character.PermisB = false;
                        }
                        player.SetPrisonTime(prisonTime);
                        player.Notify("Sanction acceptée", "Vous venez d'accepter votre sort.", Life.NotificationManager.Type.Success, 10);

                        return await Task.FromResult(true);
                    }
                    else
                    {
                        player.Notify("Erreur", "Nous n'avons pas pu appliquer votre sanction.", Life.NotificationManager.Type.Error);
                        return await Task.FromResult(false);
                    }
                }
                else
                {
                    player.Notify("Erreur", "Vous avez déjà payé cette dette à la société", Life.NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }

            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        public async void JobPoliceSanctionPanel(Player player)
        {
            var citizens = await JobPoliceCitizen.QueryAll();

            Panel panel = Context.PanelHelper.Create("Casiers judiciaire", UIPanel.PanelType.Tab, player, () => JobPoliceSanctionPanel(player));

            if(citizens != null && citizens.Count > 0)
            {
                foreach(var citizen in citizens)
                {
                    panel.AddTabLine($"{citizen.Pseudonym}", ui => JobPoliceCitizenSanctionPanel(player, citizen));
                }

                panel.NextButton("Sélectionner", () => panel.SelectTab());
            }

            panel.NextButton("Proximité", () =>
            {
                if (citizens != null && citizens.Count > 0)
                {
                    var closestPlayer = player.GetClosestPlayer();
                    if (closestPlayer != null)
                    {
                        var target = citizens.Where(c => c.CharacterId == closestPlayer.character.Id).FirstOrDefault();
                        if (target != null && target != default)
                        {
                            JobPoliceCitizenSanctionPanel(player, target);
                        }
                        else player.Notify("Casier judiciaire", "Ce citoyen est incconu de vos services.<br>Veuillez l'enregistrer, puis revenir.", Life.NotificationManager.Type.Warning);
                    } else player.Notify("Erreur","Personne n'est à votre proximité",Life.NotificationManager.Type.Error);
                } else player.Notify("Casier judiciaire", "Ce citoyen est inconnu de vos services.<br>Veuillez l'enregistrer, puis revenir.", Life.NotificationManager.Type.Warning);
            });
            panel.CloseButton();

            panel.Display();
        }
        #endregion

        #region REPLACE YOUR CLASS/TYPE AS PARAMETER
        /// <summary>
        /// Displays a panel allowing the player to select a pattern from a list of patterns.
        /// </summary>
        /// <param name="player">The player selecting the pattern.</param>
        /// <param name="patterns">The list of patterns to choose from.</param>
        /// <param name="configuring">A flag indicating if the player is configuring.</param>
        public void SelectPattern(Player player, List<JobPoliceSanctionPoint> patterns, bool configuring)
        {
            Panel panel = Context.PanelHelper.Create("Choisir un modèle", UIPanel.PanelType.Tab, player, () => SelectPattern(player, patterns, configuring));

            foreach (var pattern in patterns)
            {
                panel.AddTabLine($"{pattern.PatternName}", _ => { });
            }
            if (patterns.Count == 0) panel.AddTabLine($"Vous n'avez aucun modèle de {TypeName}", _ => { });

            if (!configuring && patterns.Count != 0)
            {
                panel.CloseButtonWithAction("Confirmer", async () =>
                {
                    if (await Context.PointHelper.CreateNPoint(player, patterns[panel.selectedTab])) return true;
                    else return false;
                });
            }
            else
            {
                panel.NextButton("Modifier", () => {
                    EditPattern(player, patterns[panel.selectedTab].Id);
                });
                panel.NextButton("Supprimer", () => {
                    ConfirmDeletePattern(player, patterns[panel.selectedTab]);
                });
            }

            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.InteractionPanel(player, AAMenu.AAMenu.menu.InteractionTabLines);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Confirms the generation of a point with a previously saved pattern.
        /// </summary>
        /// <param name="player">The player confirming the point generation.</param>
        /// <param name="pattern">The pattern to generate the point from.</param>
        public void ConfirmGeneratePoint(Player player, JobPoliceSanctionPoint pattern)
        {
            Panel panel = Context.PanelHelper.Create($"Modèle \"{pattern.PatternName}\" enregistré !", UIPanel.PanelType.Text, player, () =>
            ConfirmGeneratePoint(player, pattern));

            panel.TextLines.Add($"Voulez-vous générer un point sur votre position avec ce modèle \"{PatternName}\"");

            panel.CloseButtonWithAction("Générer", async () =>
            {
                if (await Context.PointHelper.CreateNPoint(player, pattern)) return true;
                else return false;
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion

        #region DO NOT EDIT
        /// <summary>
        /// Base panel allowing the user to choose between creating a pattern from scratch
        /// or generating a point from an existing pattern.
        /// </summary>
        /// <param name="player">The player initiating the creation or generation.</param>
        public void CreateOrGenerate(Player player)
        {
            Panel panel = Context.PanelHelper.Create($"Créer ou générer un {TypeName}", UIPanel.PanelType.Text, player, () => CreateOrGenerate(player));

            panel.TextLines.Add(mk.Pos($"{mk.Align($"{mk.Color("Générer", mk.Colors.Info)} utiliser un modèle existant. Les données sont partagés entre les points utilisant un même modèle.", mk.Aligns.Left)}", 5));
            panel.TextLines.Add("");
            panel.TextLines.Add($"{mk.Align($"{mk.Color("Créer:", mk.Colors.Info)} définir un nouveau modèle de A à Z.", mk.Aligns.Left)}");

            panel.NextButton("Créer", () =>
            {
                SetPatternData(player);
            });
            panel.NextButton("Générer", async () =>
            {
                await GetPatternData(player, false);
            });
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all patterns before redirecting to a panel allowing the user various actions (CRUD).
        /// </summary>
        /// <param name="player">The player initiating the retrieval of pattern data.</param>
        /// <param name="configuring">A flag indicating if the user is configuring.</param>
        public async Task GetPatternData(Player player, bool configuring)
        {
            var patterns = await QueryAll();
            SelectPattern(player, patterns, configuring);
        }

        /// <summary>
        /// Confirms the deletion of the specified pattern.
        /// </summary>
        /// <param name="player">The player confirming the deletion.</param>
        /// <param name="patternData">The pattern data to be deleted.</param>
        public async void ConfirmDeletePattern(Player player, PatternData patternData)
        {
            var pattern = await Query(patternData.Id);

            Panel panel = Context.PanelHelper.Create($"Supprimer un modèle de {pattern.TypeName}", UIPanel.PanelType.Text, player, () =>
            ConfirmDeletePattern(player, patternData));

            panel.TextLines.Add($"Cette suppression entrainera également celle des points.");
            panel.TextLines.Add($"Êtes-vous sûr de vouloir supprimer le modèle \"{pattern.PatternName}\" ?");

            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (await Context.PointHelper.DeleteNPointsByPattern(player, pattern))
                {
                    if (await pattern.Delete())
                    {
                        return true;
                    }
                    else
                    {
                        player.Notify("Erreur", $"Nous n'avons pas pu supprimer le modèle \"{PatternName}\"", Life.NotificationManager.Type.Error, 6);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Erreur", "Certains points n'ont pas pu être supprimés.", Life.NotificationManager.Type.Error, 6);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all NPoints before redirecting to a panel allowing various actions by the user.
        /// </summary>
        /// <param name="player">The player retrieving the NPoints.</param>
        public async Task GetNPoints(Player player)
        {
            var points = await NPoint.Query(e => e.TypeName == nameof(JobPoliceSanctionPoint));
            SelectNPoint(player, points);
        }

        /// <summary>
        /// Lists the points using this pattern.
        /// </summary>
        /// <param name="player">The player selecting the points.</param>
        /// <param name="points">The list of points to choose from.</param>
        public async void SelectNPoint(Player player, List<NPoint> points)
        {
            var patterns = await QueryAll();
            Panel panel = Context.PanelHelper.Create($"Points de type {nameof(JobPoliceSanctionPoint)}", UIPanel.PanelType.Tab, player, () => SelectNPoint(player, points));

            if (points.Count > 0)
            {
                foreach (var point in points)
                {
                    var currentPattern = patterns.FirstOrDefault(p => p.Id == point.PatternId);
                    panel.AddTabLine($"point n° {point.Id}: {(currentPattern != default ? currentPattern.PatternName : "???")}", _ => { });
                }

                panel.NextButton("Voir", () =>
                {
                    DisplayNPoint(player, points[panel.selectedTab]);
                });
                panel.NextButton("Supprimer", async () =>
                {
                    await Context.PointHelper.DeleteNPoint(points[panel.selectedTab]);
                    await GetNPoints(player);
                });
            }
            else
            {
                panel.AddTabLine($"Aucun point de ce type", _ => { });
            }
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsSettingPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Displays the information of a point and allows the user to modify it.
        /// </summary>
        /// <param name="player">The player viewing the point information.</param>
        /// <param name="point">The point to display information for.</param>
        public async void DisplayNPoint(Player player, NPoint point)
        {
            var pattern = await Query(p => p.Id == point.PatternId);
            Panel panel = Context.PanelHelper.Create($"Point n° {point.Id}", UIPanel.PanelType.Tab, player, () => DisplayNPoint(player, point));

            panel.AddTabLine($"Type: {point.TypeName}", _ => { });
            panel.AddTabLine($"Modèle: {(pattern[0] != null ? pattern[0].PatternName : "???")}", _ => { });
            panel.AddTabLine($"", _ => { });
            panel.AddTabLine($"Position: {point.Position}", _ => { });


            panel.AddButton("TP", ui =>
            {
                Context.PointHelper.PlayerSetPositionToNPoint(player, point);
            });
            panel.AddButton("Définir pos.", async ui =>
            {
                await Context.PointHelper.SetNPointPosition(player, point);
                panel.Refresh();
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion
    }
}

using Life.Network;
using Life.UI;
using SQLite;
using System.Threading.Tasks;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using mk = ModKit.Helper.TextFormattingHelper;
using System.Collections.Generic;
using System.Linq;
using JobPolice.Entities;
using ModKit.Utils;
using Life;
public class JobPoliceWantedPoint : ModKit.ORM.ModEntity<JobPoliceWantedPoint>, PatternData
{
    [AutoIncrement][PrimaryKey] public int Id { get; set; }
    public string TypeName { get; set; }
    public string PatternName { get; set; }

    //Declare your other properties here
    public int BizId { get; set; }

    [Ignore] public ModKit.ModKit Context { get; set; }

    public JobPoliceWantedPoint() { }
    public JobPoliceWantedPoint(bool isCreated)
    {
        TypeName = nameof(JobPoliceWantedPoint);
    }

    /// <summary>
    /// Applies the properties retrieved from the database during the generation of a point in the game using this model.
    /// </summary>
    /// <param name="patternId">The identifier of the pattern in the database.</param>
    public async Task SetProperties(int patternId)
    {
        var result = await Query(patternId);

        Id = patternId;
        TypeName = nameof(JobPoliceWantedPoint);
        PatternName = result.PatternName;

        //Add your other properties here
        BizId = result.BizId;
    }

    /// <summary>
    /// Contains the action to perform when a player interacts with the point.
    /// </summary>
    /// <param name="player">The player interacting with the point.</param>
    public void OnPlayerTrigger(Player player)
    {
        if (player.biz != null && player.biz.IsActivity(Life.BizSystem.Activity.Type.LawEnforcement)) JobPoliceWantedPanel(player);
        else player.Notify("Accès refusé", "Vous n'êtes pas autorisé à intéragir avec ce point", Life.NotificationManager.Type.Error);
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
        JobPoliceWantedPoint pattern = new JobPoliceWantedPoint(false);
        pattern.Context = Context;
        await pattern.SetProperties(patternId);

        Panel panel = Context.PanelHelper.Create($"Modifier un {pattern.TypeName}", UIPanel.PanelType.Tab, player, () => EditPattern(player, patternId));


        panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {pattern.PatternName}", _ => {
            pattern.SetName(player, true);
        });
        //Add tablines for your other properties here
        panel.AddTabLine($"{mk.Color("BizId:", mk.Colors.Info)} {pattern.BizId}", _ => {
            player.Notify("Refus", "Vous ne pouvez pas modifier manuellement cette valeur", Life.NotificationManager.Type.Error);
        });

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
            panel.NextButton("Suivant", async () =>
            {
                if (panel.inputText.Length >= 3)
                {
                    PatternName = panel.inputText;
                    //function to call for the following property
                    // If you want to generate your point
                    await Save();
                    ConfirmGeneratePoint(player, this);
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
    public void JobPoliceWantedPanel(Player player)
    {
        Panel panel = Context.PanelHelper.Create("Avis de recherches", UIPanel.PanelType.Tab, player, () => JobPoliceWantedPanel(player));

        panel.AddTabLine("Véhicules ", ui => JobPoliceVehicleWantedPanel(player));
        panel.AddTabLine("Citoyens", ui => JobPoliceCitizenWantedPanel(player));

        panel.NextButton("Sélectionner", () => panel.SelectTab());
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceVehicleWantedPanel(Player player)
    {
        var query = await JobPoliceVehicle.QueryAll();

        Panel panel = Context.PanelHelper.Create("Véhicules recherchés", UIPanel.PanelType.TabPrice, player, () => JobPoliceVehicleWantedPanel(player));

        if(query != null && query.Count > 0)
        {
            foreach(var v in query)
            {
                panel.AddTabLine($"{v.Plate}", $"{DateUtils.ConvertNumericalDateToString(v.CreatedAt)}", VehicleUtils.getIconId(v.ModelId), ui => JobPoliceShowVehicleWantedPanel(player, v));
            }

            panel.NextButton("Consulter", () => panel.SelectTab());

        } else panel.AddTabLine("Aucun", _ => { });

        panel.NextButton("Ajouter", async () =>
        {
            var newVehicle = new JobPoliceVehicle();
            await newVehicle.Save();
            JobPoliceAddVehicleWantedPanel(player, newVehicle.Id);
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public void JobPoliceShowVehicleWantedPanel(Player player, JobPoliceVehicle vehicle)
    {
        Panel panel = Context.PanelHelper.Create($"Véhicule {vehicle.Plate}", UIPanel.PanelType.Tab, player, () => JobPoliceShowVehicleWantedPanel(player, vehicle));

        panel.AddTabLine($"Nom du modèle: {VehicleUtils.getModelNameByModelId(vehicle.ModelId)}", _ => { });
        panel.AddTabLine($"Plaque: {vehicle.Plate}", _ => { });
        panel.AddTabLine($"Motif: {vehicle.Reason}", _ => { });
        panel.AddTabLine($"Créée le: {DateUtils.ConvertNumericalDateToString(vehicle.CreatedAt)}", _ => { });

        //button modifier
        panel.PreviousButtonWithAction("Supprimer", async () =>
        {
            if(await vehicle.Delete())
            {
                player.Notify("Succès", "Ce véhicule n'est plus recherché", NotificationManager.Type.Success);
                return true;
            } else
            {
                player.Notify("Erreur", "Nous n'avons pas pu retirer ce véhicule", NotificationManager.Type.Success);
                return false;
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceAddVehicleWantedPanel(Player player, int jobPoliceVehicleId)
    {
        var query = await JobPoliceVehicle.Query(v => v.Id == jobPoliceVehicleId);
        var vehicle = query?[0];

        Panel panel = Context.PanelHelper.Create("Ajouter un véhicule recherché", UIPanel.PanelType.Tab, player, () => JobPoliceAddVehicleWantedPanel(player, jobPoliceVehicleId));

        panel.AddTabLine($"{mk.Color("Nom du modèle:", mk.Colors.Info)} {(vehicle?.ModelId != default ? VehicleUtils.getModelNameByModelId(vehicle.ModelId) : ". . .")}", ui => JobPoliceVehicleSetModel(player, vehicle));
        panel.AddTabLine($"{mk.Color("Plaque:", mk.Colors.Info)} {(vehicle?.Plate != null ? vehicle.Plate : ". . .")}", ui => JobPoliceVehicleSetPlate(player, vehicle));
        panel.AddTabLine($"{mk.Color("Motif: ", mk.Colors.Info)} {(vehicle?.Reason != null ? vehicle.Reason : ". . .")}", ui => JobPoliceVehicleSetReason(player, vehicle));

        panel.NextButton("Modifier", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Enregistrer", async () =>
        {
            if(vehicle.ModelId != default && vehicle.Plate != null && vehicle.Reason != null)
            {
                vehicle.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                if (await vehicle.Save())
                {
                    player.Notify("Succès", "Véhicule enregistré !", NotificationManager.Type.Success);
                    return await Task.FromResult(true);
                }
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu enregistrer ce véhicule", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            } else
            {
                player.Notify("Erreur", "Fiche du véhicule recherché incomplet", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
            
        });

        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    #region vehicle setters
    public void JobPoliceVehicleSetPlate(Player player, JobPoliceVehicle vehicle)
    {
        Panel panel = Context.PanelHelper.Create("Définir la plaque d'immatriculation", UIPanel.PanelType.Input, player, () => JobPoliceVehicleSetPlate(player, vehicle));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                vehicle.Plate = panel.inputText;
                if(await vehicle.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }         
            else
            {
                player.Notify("Erreur", "Vous devez définir la plaque d'immatriculation", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public void JobPoliceVehicleSetReason(Player player, JobPoliceVehicle vehicle)
    {
        Panel panel = Context.PanelHelper.Create("Définir le motif de la recherche", UIPanel.PanelType.Input, player, () => JobPoliceVehicleSetReason(player, vehicle));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                vehicle.Reason = panel.inputText;
                if (await vehicle.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir le motif de la recherche", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public void JobPoliceVehicleSetModel(Player player, JobPoliceVehicle vehicle)
    {
        Panel panel = Context.PanelHelper.Create("Sélectionner le modèle du véhicule recherché", UIPanel.PanelType.TabPrice, player, () => JobPoliceVehicleSetModel(player, vehicle));

        foreach ((var model, int index) in Nova.v.vehicleModels.Select((model, index) => (model, index)))
        {
            if (!model.isDeprecated)
            {
                panel.AddTabLine($"{model.vehicleName}", async ui =>
                {
                    vehicle.ModelId = index;

                    if (await vehicle.Save()) panel.Previous();
                    else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                });
            }
        }

        panel.AddButton("Sélectionner", ui => panel.SelectTab());
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    #endregion

    public async void JobPoliceCitizenWantedPanel(Player player)
    {
        var query = await JobPoliceCitizen.QueryAll();

        Panel panel = Context.PanelHelper.Create("Citoyens recherchés", UIPanel.PanelType.Tab, player, () => JobPoliceCitizenWantedPanel(player));

        if (query != null && query.Count > 0)
        {
            foreach (var c in query)
            {
                panel.AddTabLine($"{c.Firstname} {c.Lastname} {mk.Color($"{DateUtils.ConvertNumericalDateToString(c.CreatedAt)}",$"{mk.Colors.Warning}")}", _ => { });
            }
            panel.NextButton("Consulter", () => panel.SelectTab());
        }
        else panel.AddTabLine("Aucun", _ => { });


        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public void JobPoliceAddCitizenWantedPanel(Player player)
    {
        var newVehicle = new JobPoliceVehicle();

        Panel panel = Context.PanelHelper.Create("Ajouter un citoyen recherché", UIPanel.PanelType.Input, player, () => JobPoliceAddCitizenWantedPanel(player));

        panel.TextLines.Add("Renseigner la plaque du véhicule");

        panel.NextButton("Continuer", () =>
        {
            if (panel.inputText.Length > 0)
            {
                newVehicle.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
                newVehicle.Plate = panel.inputText;
            }
            else player.Notify("Erreur", "Vous devez renseigner la plaque d'immatriculation", NotificationManager.Type.Error);
        });

        panel.PreviousButton();
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
    public void SelectPattern(Player player, List<JobPoliceWantedPoint> patterns, bool configuring)
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
    public void ConfirmGeneratePoint(Player player, JobPoliceWantedPoint pattern)
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

        panel.TextLines.Add($"{mk.Align($"{mk.Color("Cas particulier:", mk.Colors.Info)} afin d'éviter des conflits techniques, créer ce type de point depuis la section \"Métier\" de la société concerné.", mk.Aligns.Left)}");


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
        var points = await NPoint.Query(e => e.TypeName == nameof(JobPoliceWantedPoint));
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
        Panel panel = Context.PanelHelper.Create($"Points de type {nameof(JobPoliceWantedPoint)}", UIPanel.PanelType.Tab, player, () => SelectNPoint(player, points));

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
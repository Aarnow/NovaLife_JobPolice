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
using System;
public class JobPoliceCentralPoint : ModKit.ORM.ModEntity<JobPoliceCentralPoint>, PatternData
{
    [AutoIncrement][PrimaryKey] public int Id { get; set; }
    public string TypeName { get; set; }
    public string PatternName { get; set; }

    //Declare your other properties here
    public int BizId { get; set; }

    [Ignore] public ModKit.ModKit Context { get; set; }

    public JobPoliceCentralPoint() { }
    public JobPoliceCentralPoint(bool isCreated)
    {
        TypeName = nameof(JobPoliceCentralPoint);
    }

    /// <summary>
    /// Applies the properties retrieved from the database during the generation of a point in the game using this model.
    /// </summary>
    /// <param name="patternId">The identifier of the pattern in the database.</param>
    public async Task SetProperties(int patternId)
    {
        var result = await Query(patternId);

        Id = patternId;
        TypeName = nameof(JobPoliceCentralPoint);
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
        JobPoliceCentralPoint pattern = new JobPoliceCentralPoint(false);
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
        Panel panel = Context.PanelHelper.Create("Centrale", UIPanel.PanelType.Tab, player, () => JobPoliceWantedPanel(player));

        panel.AddTabLine("Véhicules recherchés", ui => JobPoliceVehicleWantedPanel(player));
        panel.AddTabLine("Citoyens enregistrés", ui => JobPoliceCitizenWantedPanel(player));
        panel.AddTabLine("Infractions", ui => JobPoliceOffensePanel(player));


        panel.NextButton("Sélectionner", () => panel.SelectTab());
        panel.CloseButton();

        panel.Display();
    }

    #region VEHICLES
    public async void JobPoliceVehicleWantedPanel(Player player)
    {
        var query = await JobPoliceVehicle.QueryAll();

        Panel panel = Context.PanelHelper.Create("Véhicules recherchés", UIPanel.PanelType.TabPrice, player, () => JobPoliceVehicleWantedPanel(player));

        if(query != null && query.Count > 0)
        {
            foreach(var v in query)
            {
                panel.AddTabLine($"{v.Plate}", $"{DateUtils.ConvertNumericalDateToString(v.CreatedAt)}", VehicleUtils.GetIconId(v.ModelId), ui => JobPoliceShowVehicleWantedPanel(player, v.Id));
            }

            panel.NextButton("Consulter", () => panel.SelectTab());

        } else panel.AddTabLine("Aucun", _ => { });

        panel.NextButton("Ajouter", async () =>
        {
            var newVehicle = new JobPoliceVehicle();
            newVehicle.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
            await newVehicle.Save();
            JobPoliceAddVehicleWantedPanel(player, newVehicle.Id);
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public async void JobPoliceShowVehicleWantedPanel(Player player, int jobPoliceVehicleId)
    {
        var query = await JobPoliceVehicle.Query(v => v.Id == jobPoliceVehicleId);
        var vehicle = query?[0];

        Panel panel = Context.PanelHelper.Create($"Détails du véhicule recherché", UIPanel.PanelType.Tab, player, () => JobPoliceShowVehicleWantedPanel(player, jobPoliceVehicleId));

        panel.AddTabLine($"{mk.Color("Modèle:", mk.Colors.Info)} {VehicleUtils.GetModelNameByModelId(vehicle.ModelId)}", ui => JobPoliceVehicleSetModel(player, vehicle));
        panel.AddTabLine($"{mk.Color("Plaque:", mk.Colors.Info)} {vehicle.Plate}", ui => JobPoliceVehicleSetPlate(player, vehicle));
        panel.AddTabLine($"{mk.Color("Motif:", mk.Colors.Info)} {vehicle.Reason}", ui => JobPoliceVehicleSetReason(player, vehicle));
        panel.AddTabLine($"{mk.Color("Recherché depuis le:", mk.Colors.Info)} {DateUtils.ConvertNumericalDateToString(vehicle.CreatedAt)}", ui =>
        {
            player.Notify("Erreur", "Vous ne pouvez pas modifier la date de création", NotificationManager.Type.Error);
            panel.Refresh();
        });

        panel.NextButton("Modifier", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Supprimer", async () =>
        {
            if(await vehicle.Delete())
            {
                player.Notify("Succès", "Ce véhicule n'est plus recherché", NotificationManager.Type.Success);
                return true;
            } else
            {
                player.Notify("Erreur", "Nous n'avons pas pu retirer ce véhicule", NotificationManager.Type.Error);
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

        panel.AddTabLine($"{mk.Color("Nom du modèle:", mk.Colors.Info)} {VehicleUtils.GetModelNameByModelId(vehicle.ModelId)}", ui => JobPoliceVehicleSetModel(player, vehicle));
        panel.AddTabLine($"{mk.Color("Plaque:", mk.Colors.Info)} {(vehicle?.Plate != null ? vehicle.Plate : ". . .")}", ui => JobPoliceVehicleSetPlate(player, vehicle));
        panel.AddTabLine($"{mk.Color("Motif: ", mk.Colors.Info)} {(vehicle?.Reason != null ? vehicle.Reason : ". . .")}", ui => JobPoliceVehicleSetReason(player, vehicle));

        panel.NextButton("Modifier", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Enregistrer", async () =>
        {
            if(vehicle.Plate != null && vehicle.Reason != null)
            {
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
                panel.AddTabLine($"{model.vehicleName}", "", VehicleUtils.GetIconId(index), async ui =>
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

    #endregion

    #region CITIZENS
    public async void JobPoliceCitizenWantedPanel(Player player)
    {
        var query = await JobPoliceCitizen.QueryAll();
        var queryTrie = query.OrderBy(item => item.Pseudonym).ToList();

        Panel panel = Context.PanelHelper.Create("Citoyens enregistrés", UIPanel.PanelType.Tab, player, () => JobPoliceCitizenWantedPanel(player));

        if (queryTrie != null && queryTrie.Count > 0)
        {
            foreach (var c in queryTrie)
            {
                panel.AddTabLine($"{c.Pseudonym}", ui => JobPoliceShowCitizenWantedPanel(player, c.Id));
            }
            panel.NextButton("Consulter", () => panel.SelectTab());
        }
        else panel.AddTabLine("Aucun", _ => { });

        panel.NextButton("Ajouter", async () =>
        {
            var newCitizen = new JobPoliceCitizen();
            newCitizen.CreatedAt = DateUtils.GetNumericalDateOfTheDay();
            await newCitizen.Save();
            JobPoliceAddCitizenWantedPanel(player, newCitizen.Id);
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceShowCitizenWantedPanel(Player player, int jobPoliceCitizenId)
    {
        var query = await JobPoliceCitizen.Query(v => v.Id == jobPoliceCitizenId);
        var citizen = query?[0];

        Panel panel = Context.PanelHelper.Create($"Détails du citoyen enregistré", UIPanel.PanelType.Tab, player, () => JobPoliceShowCitizenWantedPanel(player, jobPoliceCitizenId));

        panel.AddTabLine($"{mk.Color("Empreintes digitales:", mk.Colors.Info)} {(citizen.CharacterId != default ? $"[{citizen.CharacterId}]" : $"{mk.Italic("inconnu")}")}", async ui =>
        {
            var target = player.GetClosestPlayer();
            if (target != null)
            {
                citizen.CharacterId = target.character.Id;
                if (await citizen.Save()) panel.Refresh();
                else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
            }
            else
            {
                player.Notify("Condition", "\r\nLe citoyen dont vous souhaitez relever les empreintes doit être à proximité de vous", NotificationManager.Type.Warning);
                panel.Refresh();
            }
        });
        panel.AddTabLine($"{mk.Color("Pseudonym:", mk.Colors.Info)} {(citizen.Pseudonym != null ? $"{citizen.Pseudonym}" : $"{mk.Italic("à définir")}")}", ui => JobPoliceCitizenSetPseudonym(player, citizen));
        panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {(citizen.Lastname != null ? $"{citizen.Lastname}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetLastname(player, citizen));
        panel.AddTabLine($"{mk.Color("Prénom:", mk.Colors.Info)} {(citizen.Firstname != null ? $"{citizen.Firstname}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetFirstname(player, citizen));
        panel.AddTabLine($"{mk.Color("Téléphone:", mk.Colors.Info)} {(citizen.PhoneNumber != null ? $"{citizen.PhoneNumber}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetPhoneNumber(player, citizen));
        panel.AddTabLine($"{mk.Color("Genre:", mk.Colors.Info)} {(citizen.Sexe != null ? $"{citizen.Sexe}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetSexe(player, citizen));
        panel.AddTabLine($"{mk.Color("Couleur des yeux:", mk.Colors.Info)} {(citizen.EyesColor != default ? $"{citizen.EyesColor}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetEyesColor(player, citizen));
        panel.AddTabLine($"{mk.Color("Couleur de peau:", mk.Colors.Info)} {(citizen.SkinColor != null ? $"{citizen.SkinColor}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetSkinColor(player, citizen));
        panel.AddTabLine($"{mk.Color("Recherché:", mk.Colors.Info)} {(citizen.IsWanted ? "oui" : "non")}", async ui => {
            citizen.IsWanted = !citizen.IsWanted;
            if (await citizen.Save()) panel.Refresh();
            else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
        });
        if(citizen.IsWanted) panel.AddTabLine($"{mk.Color("Motif de la recherche:", mk.Colors.Info)} {(citizen.Reason != null ? $"{citizen.Reason}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetReason(player, citizen));
        panel.AddTabLine($"{mk.Color("Fichier S:", mk.Colors.Info)} {(citizen.IsDangerous ? "oui" : "non")}", async ui => {
            citizen.IsDangerous = !citizen.IsDangerous;
            if (await citizen.Save()) panel.Refresh();
            else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
        });
        panel.AddTabLine($"{mk.Color("Ajouté le:", mk.Colors.Info)} {DateUtils.ConvertNumericalDateToString(citizen.CreatedAt)}", ui =>
        {
            player.Notify("Erreur", "Vous ne pouvez pas modifier la date de création", NotificationManager.Type.Error);
            panel.Refresh();
        });
        panel.AddTabLine($"{mk.Color("Consulter le casier judiciaire", mk.Colors.Warning)}", ui => JobPoliceCitizenRecordsPanel(player, citizen.Id));

        panel.NextButton("Sélectionner", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Supprimer", async () =>
        {
            if (await citizen.Delete())
            {
                player.Notify("Succès", "Ce citoyen n'est plus référencé", NotificationManager.Type.Success);
                return true;
            }
            else
            {
                player.Notify("Erreur", "Nous n'avons pas pu retirer ce citoyen", NotificationManager.Type.Error);
                return false;
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceCitizenRecordsPanel(Player player, int citizenId)
    {
        var records = await JobPoliceRecord.Query(r => r.CitizenId == citizenId);
        var offenses = await JobPoliceOffense.QueryAll();

        Panel panel = Context.PanelHelper.Create($"Casier judiciaire", UIPanel.PanelType.TabPrice, player, () => JobPoliceCitizenRecordsPanel(player, citizenId));

        if (records != null && records.Count != 0)
        {
            foreach (var record in records)
            {
                panel.AddTabLine($"Bulletin n°{record.Id}: {(record.IsPaid ? $"{mk.Color("Payé", mk.Colors.Success)}" : $"{mk.Color("Impayé", mk.Colors.Error)}")}", $"{DateUtils.ConvertNumericalDateToString(record.CreatedAt)}", 499, ui => JobPoliceCitizenRecordDetailsPanel(player, record));
            }
            panel.NextButton("Sélectionner", () => panel.SelectTab());
        }
        else panel.AddTabLine("Aucun casier judiciaire", _ => { });

        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceCitizenRecordDetailsPanel(Player player, JobPoliceRecord jobPoliceRecord)
    {
        var offenses = await JobPoliceOffense.QueryAll();
        jobPoliceRecord.LOffenseList = ListConverter.ReadJson(jobPoliceRecord.OffenseList);
        var prisonTime = 0;
        var money = 0.0;
        var points = 0;

        Panel panel = Context.PanelHelper.Create($"Casier judiciaire n°{jobPoliceRecord.Id}", UIPanel.PanelType.TabPrice, player, () => JobPoliceCitizenRecordDetailsPanel(player, jobPoliceRecord));

        if (offenses != null && offenses.Count != 0)
        {
            if (jobPoliceRecord.LOffenseList != null && jobPoliceRecord.LOffenseList.Count != 0)
            {
                foreach (var offenseId in jobPoliceRecord.LOffenseList)
                {
                    var currentOffense = offenses.Where(o => o.Id == offenseId).FirstOrDefault();
                    if (currentOffense != null)
                    {
                        prisonTime += currentOffense.PrisonTime;
                        money += currentOffense.Money;
                        points += currentOffense.Points;
                        panel.AddTabLine($"{currentOffense.Title}", $"{currentOffense.OffenseType}", ItemUtils.GetIconIdByItemId(currentOffense.OffenseTag[currentOffense.OffenseType]), _ => { });
                    }
                }
                panel.AddTabLine($"{mk.Color("Temps de prison", mk.Colors.Info)}", $"{prisonTime} secondes", -1, _ => { });
                panel.AddTabLine($"{mk.Color("Montant de l'amende", mk.Colors.Info)}", $"{money}€", -1, _ => { });
                panel.AddTabLine($"{mk.Color("Retrait points de permis B", mk.Colors.Info)}", $"{points} points", -1, _ => { });
            }
            else player.Notify("Erreur", "Nous n'avons pas pu récupérer les offenses", Life.NotificationManager.Type.Error);
        }
        else player.Notify("Erreur", "Nous n'avons pas pu récupérer les infractions", Life.NotificationManager.Type.Error);

        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public async void JobPoliceAddCitizenWantedPanel(Player player, int jobPoliceCitizenId)
    {
        var query = await JobPoliceCitizen.Query(v => v.Id == jobPoliceCitizenId);
        var citizen = query?[0];

        Panel panel = Context.PanelHelper.Create("Ajouter un citoyen", UIPanel.PanelType.Tab, player, () => JobPoliceAddCitizenWantedPanel(player, jobPoliceCitizenId));
        
        panel.AddTabLine($"{mk.Color("Empreintes digitales:", mk.Colors.Info)} {(citizen.CharacterId != default ? $"[{citizen.CharacterId}]": $"{mk.Italic("inconnu")}")}", async ui =>
        {
            var target = player.GetClosestPlayer();
            if(target != null)
            {
                citizen.CharacterId = target.character.Id;
                if (await citizen.Save()) panel.Refresh();
                else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
            }
            else
            {
                player.Notify("Condition", "Le citoyen dont vous souhaitez relever les empreintes doit être à votre proximité", NotificationManager.Type.Info);
                panel.Refresh();
            }
        });
        panel.AddTabLine($"{mk.Color("Pseudonym:", mk.Colors.Info)} {(citizen.Pseudonym != null ? $"{citizen.Pseudonym}": $"{mk.Italic("à définir")}")}", ui => JobPoliceCitizenSetPseudonym(player, citizen));
        panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {(citizen.Lastname != null ? $"{citizen.Lastname}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetLastname(player, citizen));
        panel.AddTabLine($"{mk.Color("Prénom:", mk.Colors.Info)} {(citizen.Firstname != null ? $"{citizen.Firstname}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetFirstname(player, citizen));
        panel.AddTabLine($"{mk.Color("Téléphone:", mk.Colors.Info)} {(citizen.PhoneNumber != null ? $"{citizen.PhoneNumber}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetPhoneNumber(player, citizen));
        panel.AddTabLine($"{mk.Color("Sexe:", mk.Colors.Info)} {(citizen.Sexe != null ? $"{citizen.Sexe}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetSexe(player, citizen));
        panel.AddTabLine($"{mk.Color("Couleur des yeux:", mk.Colors.Info)} {(citizen.EyesColor != default ? $"{citizen.EyesColor}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetEyesColor(player, citizen));
        panel.AddTabLine($"{mk.Color("Couleur de peau:", mk.Colors.Info)} {(citizen.SkinColor != null ? $"{citizen.SkinColor}": $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetSkinColor(player, citizen));
        panel.AddTabLine($"{mk.Color("Recherché:", mk.Colors.Info)} {(citizen.IsWanted ? "oui":"non")}", async ui => {
            citizen.IsWanted = !citizen.IsWanted;
            if (await citizen.Save()) panel.Refresh();
            else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
        });
        if (citizen.IsWanted) panel.AddTabLine($"{mk.Color("Motif de la recherche:", mk.Colors.Info)} {(citizen.Reason != null ? $"{citizen.Reason}" : $"{mk.Italic("inconnu")}")}", ui => JobPoliceCitizenSetReason(player, citizen));
        panel.AddTabLine($"{mk.Color("Fichier S:", mk.Colors.Info)} {(citizen.IsDangerous ? "oui":"non")}", async ui => {
            citizen.IsDangerous = !citizen.IsDangerous;
            if (await citizen.Save()) panel.Refresh();
            else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette valeur", NotificationManager.Type.Error);
        });

        panel.NextButton("Modifier", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Enregistrer", async () =>
        {
            if (citizen.Pseudonym != null)
            {
                if (await citizen.Save())
                {
                    player.Notify("Succès", "Citoyen enregistré !", NotificationManager.Type.Success);
                    return await Task.FromResult(true);
                }
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu enregistrer ce citoyen", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Fiche du citoyen incomplet. Veuillez renseigner au minimum le pseudonyme.", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });

        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    #region citizen setters
    public void JobPoliceCitizenSetPseudonym(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le pseudonyme", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetPseudonym(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.Pseudonym = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetFirstname(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le prénom", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetFirstname(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.Firstname = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetLastname(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le nom", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetLastname(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.Lastname = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetPhoneNumber(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le numéro de téléphone", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetPhoneNumber(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.PhoneNumber = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetSexe(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le sexe", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetSexe(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.Sexe = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetSkinColor(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir la couleur de peau", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetSkinColor(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.SkinColor = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceCitizenSetEyesColor(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir la couleur des yeux", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetEyesColor(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.EyesColor = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }

    public void JobPoliceCitizenSetReason(Player player, JobPoliceCitizen citizen)
    {
        Panel panel = Context.PanelHelper.Create("Définir le motif de recherche", UIPanel.PanelType.Input, player, () => JobPoliceCitizenSetReason(player, citizen));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                citizen.Reason = panel.inputText;
                if (await citizen.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir une valeur", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    #endregion
    #endregion

    #region OFFENSES
    public async void JobPoliceOffensePanel(Player player)
    {
        var query = await JobPoliceOffense.QueryAll();

        Panel panel = Context.PanelHelper.Create("Infractions", UIPanel.PanelType.TabPrice, player, () => JobPoliceOffensePanel(player));

        if (query != null && query.Count > 0)
        {
            foreach (var offense in query)
            {
                panel.AddTabLine($"{(offense.Title != null ? $"{offense.Title}":"à définir")}", $"{(offense.OffenseType != null ? $"{offense.OffenseType}" : "à définir")}", offense.OffenseType != null ? ItemUtils.GetIconIdByItemId(offense.OffenseTag[offense.OffenseType]) : -1, ui => JobPoliceShowOffensePanel(player, offense.Id));
            }

            panel.NextButton("Consulter", () => panel.SelectTab());

        }
        else panel.AddTabLine("Aucun", _ => { });

        if (player.biz.OwnerId == player.character.Id)
        {
            panel.NextButton("Ajouter", async () =>
            {
                var newOffense = new JobPoliceOffense();
                await newOffense.Save();
                JobPoliceAddOffensePanel(player, newOffense.Id);
            });
        }
        
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public async void JobPoliceShowOffensePanel(Player player, int jobPoliceOffenseId)
    {
        var query = await JobPoliceOffense.Query(o => o.Id == jobPoliceOffenseId);
        var offense = query?[0];

        Panel panel = Context.PanelHelper.Create($"Détails de l'infraction", UIPanel.PanelType.Tab, player, () => JobPoliceShowOffensePanel(player, jobPoliceOffenseId));

        panel.AddTabLine($"{mk.Color("Titre:", mk.Colors.Info)} {offense.Title}", ui => JobPoliceOffenseSetTitle(player, offense));
        panel.AddTabLine($"{mk.Color("Type:", mk.Colors.Info)} {offense.OffenseType}", ui => JobPoliceOffenseSetType(player, offense));
        panel.AddTabLine($"{mk.Color("Temps de prison:", mk.Colors.Info)} {offense.PrisonTime} secondes", ui => JobPoliceOffenseSetPrisonTime(player, offense));
        panel.AddTabLine($"{mk.Color("Montant de l'amende:", mk.Colors.Info)} {offense.Money}€", ui => JobPoliceOffenseSetMoney(player, offense));
        panel.AddTabLine($"{mk.Color("Retrait points de permis B:", mk.Colors.Info)} {offense.Points}", ui => JobPoliceOffenseSetPoints(player, offense));
      
        if(player.biz.OwnerId == player.character.Id)
        {
            panel.NextButton("Modifier", () => panel.SelectTab());
            panel.PreviousButtonWithAction("Supprimer", async () =>
            {
                if (await offense.Delete())
                {
                    player.Notify("Succès", "Infraction supprimé", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu supprimer cette infraction", NotificationManager.Type.Error);
                    return false;
                }
            });
        }
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public async void JobPoliceAddOffensePanel(Player player, int jobPoliceOffenseId)
    {
        var query = await JobPoliceOffense.Query(o => o.Id == jobPoliceOffenseId);
        var offense = query?[0];

        Panel panel = Context.PanelHelper.Create("Ajouter une infraction", UIPanel.PanelType.Tab, player, () => JobPoliceAddOffensePanel(player, jobPoliceOffenseId));


        panel.AddTabLine($"{mk.Color("Titre:", mk.Colors.Info)} {(offense.Title != null ? $"{offense.Title}" : $"{mk.Italic("à définir")}")}", ui => JobPoliceOffenseSetTitle(player, offense));
        panel.AddTabLine($"{mk.Color("Type:", mk.Colors.Info)} {(offense.OffenseType != null ? $"{offense.OffenseType}" : $"{mk.Italic("à définir")}")}", ui => JobPoliceOffenseSetType(player, offense));
        panel.AddTabLine($"{mk.Color("Temps de prison:", mk.Colors.Info)} {offense.PrisonTime} secondes", ui => JobPoliceOffenseSetPrisonTime(player, offense));
        panel.AddTabLine($"{mk.Color("Montant de l'amende:", mk.Colors.Info)} {offense.Money}€", ui => JobPoliceOffenseSetMoney(player, offense));
        panel.AddTabLine($"{mk.Color("Retrait points de permis B:", mk.Colors.Info)} {offense.Points}", ui => JobPoliceOffenseSetPoints(player, offense));
        

        panel.NextButton("Modifier", () => panel.SelectTab());
        panel.PreviousButtonWithAction("Enregistrer", async () =>
        {
            if (offense.Title != null && offense.OffenseType != null)
            {
                if (await offense.Save())
                {
                    player.Notify("Succès", "Infraction enregistré !", NotificationManager.Type.Success);
                    return await Task.FromResult(true);
                }
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu enregistrer cette infraction", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Infraction incomplète", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });

        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    #region offense setters
    public void JobPoliceOffenseSetTitle(Player player, JobPoliceOffense offense)
    {
        Panel panel = Context.PanelHelper.Create("Définir le titre de l'infraction", UIPanel.PanelType.Input, player, () => JobPoliceOffenseSetTitle(player, offense));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                offense.Title = panel.inputText;
                if (await offense.Save()) return await Task.FromResult(true);
                else
                {
                    player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir le titre de l'infraction", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceOffenseSetType(Player player, JobPoliceOffense offense)
    {
        Panel panel = Context.PanelHelper.Create("Définir le type d'infraction", UIPanel.PanelType.Tab, player, () => JobPoliceOffenseSetType(player, offense));

        foreach (var type in offense.OffenseTag)
        {
            panel.AddTabLine($"{type.Key}", async ui =>
            {
                offense.OffenseType = type.Key;
                if (await offense.Save()) panel.Previous();
                else player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
            });
        }

        panel.AddButton("Sélectionner", ui => panel.SelectTab());
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceOffenseSetMoney(Player player, JobPoliceOffense offense)
    {
        Panel panel = Context.PanelHelper.Create("Définir le montant de l'amende de l'infraction", UIPanel.PanelType.Input, player, () => JobPoliceOffenseSetMoney(player, offense));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                if(double.TryParse(panel.inputText, out var amount))
                {
                    offense.Money = amount;
                    if (await offense.Save()) return await Task.FromResult(true);
                    else
                    {
                        player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                        return await Task.FromResult(false);
                    }
                } else
                {
                    player.Notify("Erreur", "format invalide", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir le titre de l'infraction", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceOffenseSetPrisonTime(Player player, JobPoliceOffense offense)
    {
        Panel panel = Context.PanelHelper.Create("Définir le temps de prison de l'infraction", UIPanel.PanelType.Input, player, () => JobPoliceOffenseSetPrisonTime(player, offense));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
               if(int.TryParse(panel.inputText, out int value))
                {
                    offense.PrisonTime = value;
                    if (await offense.Save()) return await Task.FromResult(true);
                    else
                    {
                        player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                        return await Task.FromResult(false);
                    }
                }
                else
                {
                    player.Notify("Erreur", "format invalide", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir le titre de l'infraction", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    public void JobPoliceOffenseSetPoints(Player player, JobPoliceOffense offense)
    {
        Panel panel = Context.PanelHelper.Create("Définir le retrait de points de l'infraction", UIPanel.PanelType.Input, player, () => JobPoliceOffenseSetPoints(player, offense));

        panel.PreviousButtonWithAction("Valider", async () =>
        {
            if (panel.inputText != null)
            {
                if (int.TryParse(panel.inputText, out int value))
                {
                    offense.Points = value;
                    if (await offense.Save()) return await Task.FromResult(true);
                    else
                    {
                        player.Notify("Erreur", "Nous n'avons pas pu mettre à jour cette donnée", NotificationManager.Type.Error);
                        return await Task.FromResult(false);
                    }
                }
                else
                {
                    player.Notify("Erreur", "format invalide", NotificationManager.Type.Error);
                    return await Task.FromResult(false);
                }
            }
            else
            {
                player.Notify("Erreur", "Vous devez définir le titre de l'infraction", NotificationManager.Type.Error);
                return await Task.FromResult(false);
            }
        });
        panel.PreviousButton();
        panel.CloseButton();

        panel.Display();
    }
    #endregion
    #endregion

    #endregion

    #region REPLACE YOUR CLASS/TYPE AS PARAMETER
    /// <summary>
    /// Displays a panel allowing the player to select a pattern from a list of patterns.
    /// </summary>
    /// <param name="player">The player selecting the pattern.</param>
    /// <param name="patterns">The list of patterns to choose from.</param>
    /// <param name="configuring">A flag indicating if the player is configuring.</param>
    public void SelectPattern(Player player, List<JobPoliceCentralPoint> patterns, bool configuring)
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
    public void ConfirmGeneratePoint(Player player, JobPoliceCentralPoint pattern)
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
        var points = await NPoint.Query(e => e.TypeName == nameof(JobPoliceCentralPoint));
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
        Panel panel = Context.PanelHelper.Create($"Points de type {nameof(JobPoliceCentralPoint)}", UIPanel.PanelType.Tab, player, () => SelectNPoint(player, points));

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
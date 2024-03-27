using Life;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Internal;

namespace JobPolice
{
    public class JobPolice : ModKit.ModKit
    {
        public JobPolice(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitEntities();
            InsertMenu();
            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public void InitEntities()
        {
        }

        public void InsertMenu()
        {
        }
    }
}

using Barotrauma.MoreLevelContent.Shared.Utils;

namespace MoreLevelContent.Shared.Generation
{
    public abstract class Director<DirectorType, ModuleType> : Singleton<DirectorType> 
        where DirectorType : class 
        where ModuleType : DirectorModule<ModuleType>
    {
        
    }
}

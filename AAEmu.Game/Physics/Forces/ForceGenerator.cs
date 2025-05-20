using Jitter2;

namespace AAEmu.Game.Physics.Forces;

/// <summary>
/// Base class for physic effect.
/// </summary>
public class ForceGenerator
{

    /// <summary>
    /// 
    /// </summary>
    protected World world;

    private readonly World.WorldStep _preStep;
    private readonly World.WorldStep _postStep;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="world"></param>
    public ForceGenerator(World world)
    {
        this.world = world;


        _preStep = new World.WorldStep(PreStep);
        _postStep = new World.WorldStep(PostStep);

        world.PostStep += _postStep;
        world.PreStep += _preStep;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timeStep"></param>
    public virtual void PreStep(float timeStep)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timeStep"></param>
    public virtual void PostStep(float timeStep)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public void RemoveEffect()
    {
        world.PostStep -= _postStep;
        world.PreStep -= _preStep;
    }
}

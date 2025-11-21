public interface IPawnState
{
    void Enter(PawnContext ctx);
    void Execute(PawnContext ctx);
    void Exit(PawnContext ctx);
}
using ElectionNode;
using Grpc.Core;

public class ElectionServiceImpl : ElectionService.ElectionServiceBase
{
    private readonly BullyElectionService _electionService;
    private readonly IConfiguration _config;

    public ElectionServiceImpl(
        BullyElectionService electionService,
        IConfiguration config)
    {
        _electionService = electionService;
        _config = config;
    }

    public override async Task<ElectionResponse> SendElection(
        ElectionRequest request,
        ServerCallContext context)
    {
        var nodeId = _config.GetValue<int>("NodeId");

        Console.WriteLine($"Recebi eleição do nó {request.NodeId}");

        if (request.NodeId < nodeId)
        {
            _ = Task.Run(() => _electionService.StartElection());
        }

        return new ElectionResponse { Received = true };
    }

    public override Task<LeaderResponse> AnnounceLeader(
        LeaderRequest request,
        ServerCallContext context)
    {
        Console.WriteLine($"Novo líder anunciado: {request.LeaderId}");

        _electionService.SetLeader(request.LeaderId);

        return Task.FromResult(new LeaderResponse { Ack = true });
    }
    
    public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context) {
            return Task.FromResult(new PingResponse { Ok = true });
    }
    
    public override Task<OkResponse> SendOk(
        OkRequest request,
        ServerCallContext context)
    {
        Console.WriteLine($"Recebi OK do nó {request.NodeId}");

        return Task.FromResult(new OkResponse { Ack = true });
    }
    
    public override Task<LeaderReply> GetLeader(
        LeaderQuery request,
        ServerCallContext context)
    {
        return Task.FromResult(new LeaderReply
        {
            LeaderId = _electionService.LeaderId
        });
    }
}
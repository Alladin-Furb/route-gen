using ElectionNode;
using Grpc.Net.Client;

public class BullyElectionService
{
    private readonly IConfiguration _config;
    private readonly int _nodeId;
    private readonly List<NodeInfo> _nodes;
    private readonly HttpClientHandler _httpHandler;

    private bool _electionInProgress = false;

    public int LeaderId { get; private set; }

    public BullyElectionService(IConfiguration config)
    {
        _config = config;

        _httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _nodeId = config.GetValue<int>("NodeId");
        _nodes = _config.GetSection("Nodes").Get<List<NodeInfo>>() ?? new();
    }

    public async Task StartElection()
    {
        if (_electionInProgress)
            return;

        _electionInProgress = true;

        Console.WriteLine($"Node {_nodeId} iniciou eleição");

        var higherNodes = _nodes.Where(n => n.Id > _nodeId);

        bool someoneResponded = false;

        foreach (var node in higherNodes)
        {
            try
            {
                var channel = GrpcChannel.ForAddress(node.Address, new()
                {
                    HttpHandler = _httpHandler
                });

                var client = new ElectionService.ElectionServiceClient(channel);

                var response = await client.SendElectionAsync(
                    new ElectionRequest { NodeId = _nodeId });

                if (response.Received)
                {
                    someoneResponded = true;
                    Console.WriteLine($"Node {node.Id} respondeu eleição");
                }
            }
            catch
            {
                Console.WriteLine($"Node {node.Id} não respondeu");
            }
        }

        if (!someoneResponded)
        {
            Console.WriteLine($"Nenhum nó maior respondeu. Node {_nodeId} será líder.");
            await BecomeLeader();
        }

        _electionInProgress = false;
    }

    private async Task BecomeLeader()
    {
        LeaderId = _nodeId;

        Console.WriteLine($"Node {_nodeId} virou líder");

        foreach (var node in _nodes.Where(n => n.Id != _nodeId))
        {
            try
            {
                var channel = GrpcChannel.ForAddress(node.Address, new()
                {
                    HttpHandler = _httpHandler
                });

                var client = new ElectionService.ElectionServiceClient(channel);

                await client.AnnounceLeaderAsync(
                    new LeaderRequest { LeaderId = _nodeId });
            }
            catch
            {
                Console.WriteLine($"Node {node.Id} não recebeu anúncio de líder");
            }
        }
    }

    public void SetLeader(int leaderId)
    {
        if (leaderId >= LeaderId)
        {
            LeaderId = leaderId;
            Console.WriteLine($"Node {_nodeId} registrou novo líder: {leaderId}");
        }
    }

    public async Task MonitorLeader()
    {
        while (true)
        {
            await Task.Delay(3000);

            if (LeaderId == 0 || LeaderId == _nodeId)
                continue;

            var leader = _nodes.FirstOrDefault(n => n.Id == LeaderId);

            if (leader == null)
                continue;

            try
            {
                var channel = GrpcChannel.ForAddress(leader.Address, new()
                {
                    HttpHandler = _httpHandler
                });

                var client = new ElectionService.ElectionServiceClient(channel);

                await client.PingAsync(new PingRequest());
            }
            catch
            {
                Console.WriteLine($"Node {_nodeId} detectou falha do líder {LeaderId}");
                LeaderId = 0;
                await StartElection();
            }
        }
    }

    public async Task DiscoverLeader()
    {
        if (LeaderId != 0)
            return;

        foreach (var node in _nodes.Where(n => n.Id != _nodeId))
        {
            try
            {
                var channel = GrpcChannel.ForAddress(node.Address, new()
                {
                    HttpHandler = _httpHandler
                });

                var client = new ElectionService.ElectionServiceClient(channel);

                var response = await client.GetLeaderAsync(new LeaderQuery());

                if (response.LeaderId != 0)
                {
                    LeaderId = response.LeaderId;
                    Console.WriteLine($"Node {_nodeId} descobriu líder: {LeaderId}");
                    return;
                }
            }
            catch
            {
            }
        }

        if (LeaderId == 0)
        {
            Console.WriteLine($"Node {_nodeId} não encontrou líder, iniciando eleição");
            await StartElection();
        }
    }
}
open Protocol
open Protocol.MessageHandling

type Client = { Id:int; Connection:System.IO.StreamWriter }

let broadcastHandler (streamWriter: System.IO.StreamWriter list) (msg:string) =
    async {
        for sw in streamWriter do
            do! sw.WriteAsync(msg) |> Async.AwaitTask
            do! sw.FlushAsync() |> Async.AwaitTask
    }

let privateMsgHandler (streamWriter: System.IO.StreamWriter) (msg:string) = 
    async{
         do! streamWriter.WriteAsync(msg) |> Async.AwaitTask
         do! streamWriter.FlushAsync() |> Async.AwaitTask
    }

type ConnectionCommand =
    | GetAll of AsyncReplyChannel<Client list>
    | GetById of int * AsyncReplyChannel<Client>
    | AddClient of System.Net.Sockets.TcpClient

let connectionHandler =
    new MailboxProcessor<ConnectionCommand>(fun inbox ->
    
    let mutable clients = []

    let rec loop id =
        async {
            let! command = inbox.Receive()

            match command with
            | AddClient con -> 
                let client = {Id=id; Connection= new System.IO.StreamWriter(con.GetStream())}
                clients <- [client] @ clients
            | GetAll replyChannel -> replyChannel.Reply clients
            | GetById (id, replyChannel) -> replyChannel.Reply (clients |> List.find (fun c -> c.Id = id))

            return! loop (id + 1) 
        }

    loop 0)

let getStreamReaderAgent() =
    new MailboxProcessor<Client>(fun inbox ->

        let broadcast = 
            (fun msg ->
                 let clients = connectionHandler.PostAndReply(ConnectionCommand.GetAll)
                               |> List.map(fun cl -> cl.Connection)
                 broadcastHandler clients msg)
       
        let privateMsg id =
            (fun msg ->
                let client = connectionHandler.PostAndReply(fun replyChannel -> ConnectionCommand.GetById (id ,replyChannel)).Connection
                msg
                |> privateMsgHandler client)

        async {
            let! client = inbox.Receive()

            let streamReader = new System.IO.StreamReader(client.Connection.BaseStream)

            while true do

                let! msg = streamReader.ReadLineAsync() |> Async.AwaitTask

                do! Protocol.MessageHandling.handleMessage broadcast privateMsg msg

                printfn "got msg from %i: %s" client.Id msg
        })

        todo start readerAgent

let connectionListener =
    new MailboxProcessor<unit>(fun _ ->

        let listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Parse("0.0.0.0"), 8888)

        connectionHandler.Start()

        listener.Start()

        let rec listenLoop() =

            async{
                let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask

                connectionHandler.Post (ConnectionCommand.AddClient client)

                let readerAgent = getStreamReaderAgent()
                readerAgent.Start()
                

                printfn "client connected: %s" (client.Client.LocalEndPoint.ToString())

                return! listenLoop()
            }
        listenLoop())
 

[<EntryPoint>]

let main _ =

    connectionListener.Start()

    System.Console.ReadLine() |> ignore

    0
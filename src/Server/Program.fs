open Protocol
open Protocol.MessageHandling
open System.Linq

type Client = { Id:int; Connection:System.IO.StreamReader }

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

type ReaderCommand =
        | StartListening of Client
        | GetBaseStream of AsyncReplyChannel<System.IO.Stream>

type ConnectionCommand =
    | GetAll of AsyncReplyChannel<System.IO.StreamWriter list>
    | GetById of int * AsyncReplyChannel<System.IO.StreamWriter>
    | AddClient of MailboxProcessor<ReaderCommand> * System.IO.StreamReader


let connectionHandler =
    new MailboxProcessor<ConnectionCommand>(fun inbox ->
    
    let clients = new System.Collections.Generic.Dictionary<int, MailboxProcessor<ReaderCommand>>()

    let rec loop id =
        async {
            let! command = inbox.Receive()

            match command with
            | AddClient (connectionHandler, streamReader) ->
                connectionHandler.Start()
                let client = {Id=id; Connection=streamReader}
                clients.Add(client.Id, connectionHandler)
                connectionHandler.Post(ReaderCommand.StartListening client)
            
            | GetAll replyChannel ->
                let writers = clients
                                .Values
                                .Select(fun cl -> cl.PostAndReply(ReaderCommand.GetBaseStream))
                                .Select(fun stream -> new System.IO.StreamWriter(stream))
                                .ToList()
                                |> List.ofSeq

                replyChannel.Reply writers
            
            | GetById (id, replyChannel) -> replyChannel.Reply (new System.IO.StreamWriter(clients.[id].PostAndReply(ReaderCommand.GetBaseStream)))

            return! loop (id + 1) 
        }

    loop 0)


let getStreamReaderAgent() =
    new MailboxProcessor<ReaderCommand>(fun inbox ->
        let mutable baseStream = null

        let broadcast = 
            (fun msg ->
                 let clients = connectionHandler.PostAndReply(ConnectionCommand.GetAll)
                 broadcastHandler clients msg)
       
        let privateMsg id =
            (fun msg ->
                let writer = connectionHandler.PostAndReply(fun replyChannel -> ConnectionCommand.GetById(id, replyChannel))
                msg
                |> privateMsgHandler writer)

        let messageHandler = Protocol.MessageHandling.handleMessage broadcast privateMsg

        let rec loop() = 
            async {
                let! command = inbox.Receive()

                match command with
                | GetBaseStream reply -> reply.Reply(baseStream)

                | StartListening client ->
                    baseStream <- client.Connection.BaseStream
                    async {
                        let streamReader = client.Connection

                        while true do
                            let! msg = streamReader.ReadLineAsync() |> Async.AwaitTask
                            printfn "got msg from %i: %s" client.Id msg 
                            do! messageHandler msg

                    } |> Async.Start
                return! loop()
            }
        loop())


let connectionListener =
    new MailboxProcessor<unit>(fun _ ->

        let listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Parse("0.0.0.0"), 8888)

        connectionHandler.Start()

        listener.Start()

        let rec listenLoop() =

            async{
                let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask

                let addCommand = 
                    let agent = getStreamReaderAgent()
                    let reader =  new System.IO.StreamReader(client.GetStream())
                    ConnectionCommand.AddClient (agent, reader)

                connectionHandler.Post addCommand

                printfn "client connected: %s" (client.Client.LocalEndPoint.ToString())

                return! listenLoop()
            }
        listenLoop())
 

[<EntryPoint>]

let main _ =

    connectionListener.Start()

    System.Console.ReadLine() |> ignore

    0
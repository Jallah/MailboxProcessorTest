open Protocol
open Protocol.MessageHandling
open System.Linq

type Client = { Id:int; Connection:System.IO.Stream }

type StreamHandlerCommand =
        | StartListening of Client
        | GetBaseStream of AsyncReplyChannel<System.IO.Stream>
        | WriteMessage of string

let broadcastHandler (clients: MailboxProcessor<StreamHandlerCommand> list) (senderId:string) (msg:string) =
    async {
        for client in clients do
            client.Post(WriteMessage (sprintf "%s -> %s" senderId msg))
    }

let privateMsgHandler (client:  MailboxProcessor<StreamHandlerCommand>) (senderId:string) (msg:string) = 
    async{
         client.Post(WriteMessage (sprintf "%s -> %s" senderId msg))
    }

type ConnectionHandlerCommand =
    | GetAll of AsyncReplyChannel<MailboxProcessor<StreamHandlerCommand> list>
    | GetById of int * AsyncReplyChannel<MailboxProcessor<StreamHandlerCommand>>
    | AddClient of MailboxProcessor<StreamHandlerCommand> * System.IO.Stream


let connectionHandler =
    new MailboxProcessor<ConnectionHandlerCommand>(fun inbox ->
    
    let clients = new System.Collections.Generic.Dictionary<int, MailboxProcessor<StreamHandlerCommand>>()

    let rec loop id =
        async {
            let! command = inbox.Receive()

            match command with
            | AddClient (conHandler, stream) ->
                conHandler.Start()
                let client = {Id=id; Connection=stream}
                clients.Add(client.Id, conHandler)
                conHandler.Post(StartListening client)
                
                return! loop (id + 1) 

            | GetAll replyChannel ->
                let writers = clients
                                .Values
                                |> List.ofSeq

                replyChannel.Reply writers
                return! loop (id)
            
            | GetById (id, replyChannel) ->
                replyChannel.Reply (clients.[id])
                return! loop (id)
        }
    loop 0)


let getStreamHandlerAgent() =
    new MailboxProcessor<StreamHandlerCommand>(fun inbox ->
        
        let mutable baseStream = null
        let mutable writer = null

        let broadcast = 
            (fun senderId msg ->
                 let clients = connectionHandler.PostAndReply(GetAll)
                 broadcastHandler clients senderId msg)
       
        let privateMsg recieverId =
            (fun senderId msg ->
                let client = connectionHandler.PostAndReply(fun replyChannel -> GetById(recieverId, replyChannel))
                msg
                |> privateMsgHandler client senderId )

        let messageHandler = Protocol.MessageHandling.handleMessage broadcast privateMsg

        let rec loop() = 
            async {
                let! command = inbox.Receive()

                match command with
                | GetBaseStream reply -> reply.Reply(baseStream)

                | StartListening client ->

                    baseStream <- client.Connection
                    writer <- new System.IO.StreamWriter(client.Connection)

                    async {
                        let streamReader = new System.IO.StreamReader(client.Connection)
                        while true do
                            let! msg = streamReader.ReadLineAsync() |> Async.AwaitTask
                            printfn "got msg from %i: %s" client.Id msg 
                            do! messageHandler (client.Id.ToString()) msg
                    } |> Async.Start

                | WriteMessage msg ->

                    async{
                        match writer = null with
                        | false ->
                            do! writer.WriteLineAsync(msg) |> Async.AwaitTask
                            do! writer.FlushAsync() |> Async.AwaitTask
                        | true -> ()
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
                    let agent = getStreamHandlerAgent()
                    let stream =  client.GetStream()
                    AddClient (agent, stream)

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
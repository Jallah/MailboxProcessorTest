open Protocol
open Protocol.MessageHandling
open System.Linq

type Agent<'a> = MailboxProcessor<'a>

type Client = { Id:string; Connection:System.IO.Stream }

type StreamHandlerCommand =
        | StartListening of Client
        | GetBaseStream of AsyncReplyChannel<System.IO.Stream>
        | WriteMessage of string

type ConnectionHandlerCommand =
    | GetAll of AsyncReplyChannel<Agent<StreamHandlerCommand> list>
    | GetById of string * AsyncReplyChannel<Option<Agent<StreamHandlerCommand>>>
    | AddClient of string * Agent<StreamHandlerCommand> * System.IO.Stream


(*
        Message handler
*)
let broadcastHandler (clients: Agent<StreamHandlerCommand> list) (senderId:string) (msg:string) =
    async {
        for client in clients do
            client.Post(WriteMessage (sprintf "%s -> %s" senderId msg))
    }

let privateMsgHandler (client:  Agent<StreamHandlerCommand>) (senderId:string) (msg:string) = 
    async{
         client.Post(WriteMessage (sprintf "%s -> %s" senderId msg))
    }


let connectionHandler =
    new Agent<ConnectionHandlerCommand>(fun inbox ->
    
    let clients = new System.Collections.Generic.Dictionary<string, Agent<StreamHandlerCommand>>()

    let rec loop() =
        async {
            let! command = inbox.Receive()

            match command with
            | AddClient (id, conHandler, stream) ->
                conHandler.Start()
                let client = {Id=id; Connection=stream}
                clients.Add(client.Id, conHandler)
                conHandler.Post(StartListening client)
                
            | GetAll replyChannel ->
                clients.Values
                    |> List.ofSeq
                    |> replyChannel.Reply 
            
            | GetById (id, replyChannel) ->
                match clients.ContainsKey(id) with
                | false -> None
                | true   -> Some clients.[id]
                |> replyChannel.Reply

            return! loop()
        }
    loop())


let getStreamHandlerAgent() =
    new Agent<StreamHandlerCommand>(fun inbox ->
        
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
                |> privateMsgHandler client senderId)

        //let login id =

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
                        match isNull writer with
                        | false ->
                            do! writer.WriteLineAsync(msg) |> Async.AwaitTask
                            do! writer.FlushAsync() |> Async.AwaitTask
                        | true -> ()
                    } |> Async.Start
                return! loop()
            }
        loop())


let connectionListener =
    new Agent<unit>(fun _ ->

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
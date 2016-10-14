open Protocol
open Protocol.MessageHandling
open System

type ClientCommands =
    | SendMessage of string
    | Login of string

type Agent<'a> = MailboxProcessor<'a>

let cprintfn(rnd: System.Random) msg =
    let color:ConsoleColor = enum (rnd.Next(1, 14))
    Console.ForegroundColor <- color
    printfn "%s" msg

let Listener = 
    new Agent<IO.StreamReader>(fun inbox ->

        let printmsg = cprintfn (new Random())
    
        let rec listenLoop() =
            async{

                let! reader = inbox.Receive()

                while true do
                    let! msg = reader.ReadLineAsync() |> Async.AwaitTask
                    printmsg msg

                return! listenLoop()
            }
        listenLoop())

let Agent =
    new Agent<ClientCommands>(fun inbox ->

                let tcpClient = new System.Net.Sockets.TcpClient()

                tcpClient.Connect("172.30.215.102", 8888)

                let stream = tcpClient.GetStream();

                let streamWriter = new System.IO.StreamWriter(stream)

                let streamReader = new System.IO.StreamReader(stream)

                Listener.Start()
                
                Listener.Post(streamReader)

                let rec loop() =

                        async {

                            let! cmd =  inbox.Receive()

                            match cmd with
                            | SendMessage msg ->
                                let msgToSend = Broadcast ("", msg) //MessageType.Private(0, msg)
                                let serializedMsg = serializeMessage msgToSend
                                do! streamWriter.WriteLineAsync(serializedMsg) |> Async.AwaitTask
                                do! streamWriter.FlushAsync() |> Async.AwaitTask

                            | Login name ->
                                let login = serializeMessage (Login name)
                                do! streamWriter.WriteLineAsync(login) |> Async.AwaitTask
                                do! streamWriter.FlushAsync() |> Async.AwaitTask

                            return! loop()
                        }
                loop())


[<EntryPoint>]
let main _ =
    
    Agent.Start()
    
    let rec login() = 
        printf "login as: "
        let loginAs = System.Console.ReadLine();
        
        match loginAs with
        | name when String.IsNullOrWhiteSpace name ->
             printfn "not allowed"
             login()
        | _ -> Agent.Post(Login loginAs)    
    
    login()

    while true do

        let msg = System.Console.ReadLine()

        Agent.Post(SendMessage msg)

    0
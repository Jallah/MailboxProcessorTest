open Protocol
open Protocol.MessageHandling

let Listener = 
    new MailboxProcessor<System.IO.StreamReader>(fun inbox ->

        let rec listenLoop() =
            async{

                let! reader = inbox.Receive()

                while true do
                    let! msg = reader.ReadLineAsync() |> Async.AwaitTask
                    printfn "got user message: %s" msg

                return! listenLoop()
            }
        listenLoop())

let Agent =
    new MailboxProcessor<string>(fun inbox ->

                let tcpClient = new System.Net.Sockets.TcpClient()

                tcpClient.Connect("127.0.0.1", 8888)

                let stream = tcpClient.GetStream();

                let streamWriter = new System.IO.StreamWriter(stream)

                let streamReader = new System.IO.StreamReader(stream)

                Listener.Start()
                
                Listener.Post(streamReader)

                let rec loop() =

                        async {

                            let! msg =  inbox.Receive()

                            let msgToSend = MessageType.Broadcast msg

                            let serializedMsg = serializeMessage msgToSend

                            do! streamWriter.WriteLineAsync(serializedMsg) |> Async.AwaitTask

                            do! streamWriter.FlushAsync() |> Async.AwaitTask

                            printfn "send msg %s" msg

                            return! loop()
                        }
                loop())

           

[<EntryPoint>]

let main _ =

    Agent.Start()

    while true do

        let msg = System.Console.ReadLine()

        Agent.Post(msg)

    0
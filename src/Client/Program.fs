let Agent =
    new MailboxProcessor<string>(fun inbox ->

                let tcpClient = new System.Net.Sockets.TcpClient()

                tcpClient.Connect("127.0.0.1", 8888)

                let streamWriter = new System.IO.StreamWriter(tcpClient.GetStream())

                let rec loop() =

                        async {

                            let! msg = inbox.Receive()

                            do! streamWriter.WriteLineAsync(msg) |> Async.AwaitTask

                            do! streamWriter.FlushAsync() |> Async.AwaitTask

                            printfn "send msg %s" msg

                            return! loop()
                        }
                loop())

           

[<EntryPoint>]

let main argv =

    Agent.Start()

    while true do

        let msg = System.Console.ReadLine()

        Agent.Post(msg)

    0
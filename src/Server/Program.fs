
type Client = { Id:int; Connection:System.Net.Sockets.TcpClient }

let getStreamReaderAgent() =
    new MailboxProcessor<Client>(fun inbox ->

        async {
            let! client = inbox.Receive()

            let streamReader = new System.IO.StreamReader(client.Connection.GetStream())

            while true do

                let! msg = streamReader.ReadLineAsync() |> Async.AwaitTask

                printfn "got msg from %i: %s" client.Id msg
        })


let connectionHandler =
    new MailboxProcessor<System.Net.Sockets.TcpClient>(fun inbox ->
    
    let rec loop id clients=
        async {
            let! con = inbox.Receive()

            let client = {Id=id; Connection=con}

            let readerAgent = getStreamReaderAgent()

            readerAgent.Start()

            readerAgent.Post client

            return! loop (id + 1) ([client] @ clients)
        }

    loop 0 [])


let connectionListener =
    new MailboxProcessor<unit>(fun _ ->

        let listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Parse("0.0.0.0"), 8888)

        connectionHandler.Start()

        listener.Start()

        let rec listenLoop() =

            async{
                let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask

                connectionHandler.Post client

                printfn "client connected: %s" (client.Client.LocalEndPoint.ToString())

                return! listenLoop()
            }
        listenLoop())
 

[<EntryPoint>]

let main _ =

    connectionListener.Start()

    System.Console.ReadLine() |> ignore

    0
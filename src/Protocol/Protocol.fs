namespace Protocol

type MessageType =
    | Broadcast of string * string
    | Private of string * string
    | Login of string
    | LoginError of string
    | LoginSuccess of string

type BroadCastMessage = {sender:string; message:string}
type PrivateMessage = {recipient:string; message:string}
type LoginMessage = {name:string}
type LoginErrorMessage = {message:string}
type LoginSuccessMessage = {message:string}

type Message = {message:System.Type; handler:obj -> Async<unit>}
type Message<'a> = {message:'a; messageHandler:'a -> Async<unit>}

module MessageHandling =

    let settings = let s = new Newtonsoft.Json.JsonSerializerSettings()
                   s.TypeNameHandling <- Newtonsoft.Json.TypeNameHandling.All
                   s

    let toUntyped<'a> (message:Message<'a>) : Message =
        let { message=msg; messageHandler=handleFunc } = message
        { message=msg.GetType(); handler=fun (arg:obj) -> handleFunc (arg :?> 'a) }

        
    let logHandler<'a> (msg:'a) = async { printfn "MSG: %A" msg }

    let broadCastHandler msg =  async {
                                    let {sender=_; message=broadcast} = msg
                                    printfn "Broadcast: %s" broadcast
                                }

    let mutable Messages = []

   // let registerMessage msg()

//    let Messages = [ {message=typeof<BroadCastMessage>; handler= toUntyped broadCastHandler }
//                     {message=typeof<PrivateMessage>; handler=logHandler }
//                     {message=typeof<LoginMessage>; handler=logHandler }
//                     {message=typeof<LoginErrorMessage>; handler=logHandler }
//                     {message=typeof<LoginSuccessMessage>; handler=logHandler }]

    let getMessageByType msgType = 
                Messages
                |> List.find (fun msg -> msg.message = msgType.GetType())

    let getMsgHandler jsonString =
        let deserializedMsg = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString, settings);
        (getMessageByType deserializedMsg).handler deserializedMsg
    

    let handleMessage broadcastHandler privateHandler loginHandler loginErrorHandler loginSuccessHandler senderId jsonString =
                try
                    match Newtonsoft.Json.JsonConvert.DeserializeObject<MessageType>(jsonString, settings) with
                    | Broadcast (_, msg)        -> broadcastHandler senderId msg
                    | Private (recieverId, msg) -> privateHandler recieverId senderId msg
                    | Login Id                  -> loginHandler Id
                    | LoginError msg            -> loginErrorHandler msg
                    | LoginSuccess msg          -> loginSuccessHandler msg

                with
                |   ex -> 
                    printfn "%s" (ex.ToString())
                    failwith ex.Message
     

    let serializeMessage msg =
        Newtonsoft.Json.JsonConvert.SerializeObject(msg,settings)
module Client

open System
open System.IO
open System.Text

open Elmish
open Elmish.React

//open Fable.Fetch
open Thoth.Json

open Fable.React
open Fable.React.Props
open Fable.Core.JsInterop
open Shared
//open Fulma.FontAwesome

open Browser.Dom
open Browser.Types

open Fulma
open Fable.FontAwesome

module Server =

    open Fable.Remoting.Client

    let csbarApi : ICSBarApi = 
        Remoting.createApi()
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.buildProxy<ICSBarApi>


// The model holds data that you want to keep track of while the application is running
type State =
|PinInput
|TickSelection
|BeverageSelection
|TickPerformed
|ErrorState


type ErrorMessage =
|NoError
|IncorrectPin of string
|TickPostFailed of string


type Model = {
    State               : State
    Pin                 : string option
    User                : Person option
    ErrorMsg            : ErrorMessage
    BeverageAmount      : int
    Balance             : float
    TickedTrade         : string*int*string
}


// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
//These messages only change stuff on the client side without server communication
|PinNumberAdd of string
|ClearPin
|BeverageSelectionWindow
|ChangeBeverageAmount of int
|Reset
//These messages cause communication with the server
//add some hash security
|ConfirmPinRequest of string
|ConfirmPinResponse of Result<Person,exn>
//add some hash security
|GetBalanceRequest of string
|GetBalanceResponse of Result<float,exn>
//add some hash security
|Tick of string*string*int*string
|TickResponse of Result<string*int*string,exn>



let initialModel = {
    State                   = State.PinInput
    Pin                     = None
    User                    = None
    ErrorMsg                = NoError
    BeverageAmount          = 1
    Balance                 = 0.
    TickedTrade             = "",-1,""
}

module RequestHelpers =

    let createCheckPinCmd (pin:string) =
        Cmd.OfAsync.either
            Server.csbarApi.ConfirmPin
            pin
            (Ok >> ConfirmPinResponse)
            (Error >> ConfirmPinResponse)

    let createTickCmd (userName:string) (tradeName:string) (amount:int) (extid:string) =
        Cmd.OfAsync.either
            (Server.csbarApi.Tick userName tradeName amount)
            extid
            (Ok >> TickResponse)
            (Error >> TickResponse)

    let createGetBalanceCmd (name:string) =
        Cmd.OfAsync.either
            Server.csbarApi.GetBalance
            name
            (Ok >> GetBalanceResponse)
            (Error >> GetBalanceResponse)



// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    initialModel, Cmd.none


// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match msg with
    |Reset ->
        initialModel,Cmd.none
    
    //add a number to the current pin by key press
    |PinNumberAdd i -> 
        let updatedPin = 
            match currentModel.Pin with
            |Some p -> sprintf "%s%s" p i
            |_ -> i
        let updatedModel =
            {currentModel with Pin = Some updatedPin}
        updatedModel, Cmd.none

    //reset the pin numbers of the current model
    |ClearPin -> 
        let updatedModel = 
            {currentModel with Pin = None}
        updatedModel, Cmd.none

    //Send get request to server to confirm the pin input.
    |ConfirmPinRequest pin -> 
        currentModel,RequestHelpers.createCheckPinCmd pin

    //If the pin was correct, proceed to tick selection
    |ConfirmPinResponse (Ok personRes) ->
        let updatedModel =
            {currentModel with User = Some personRes; State=TickSelection}
        updatedModel, Cmd.none

    //If the pin was incorrect, display error and reset model
    |ConfirmPinResponse (Error e) ->
        let updatedModel =
            {initialModel with State=ErrorState;ErrorMsg = IncorrectPin e.Message}
        updatedModel, Cmd.none 

    |ChangeBeverageAmount amt ->
        let updatedModel = 
            {currentModel with BeverageAmount = if amt >= 1 then amt else 1}
        updatedModel,Cmd.none
    
    |BeverageSelectionWindow ->
        let updatedModel = 
            {currentModel with State = BeverageSelection}
        updatedModel,Cmd.none        
    
    //Send post request to server to tick a single coffee for the models current user
    |Tick (userName,tradeName,amount,extid) ->
        currentModel,RequestHelpers.createTickCmd userName tradeName amount extid

    //If the tick was successfull, set the models state respectively
    |TickResponse (Ok tickRes) ->
        let updatedModel =
            {currentModel with  TickedTrade = tickRes}
        let usr,_,_ = tickRes
        updatedModel,Cmd.ofMsg (GetBalanceRequest usr)

    |TickResponse (Error e) ->
        let updatedModel =
            {currentModel with State=ErrorState;ErrorMsg = TickPostFailed e.Message}
        updatedModel,Cmd.none        

    |GetBalanceRequest usrName ->
        currentModel,RequestHelpers.createGetBalanceCmd usrName

    |GetBalanceResponse (Ok bal) ->
        let updatedModel =
            {currentModel with State = TickPerformed; Balance=bal}
        updatedModel,Cmd.none        

    |GetBalanceResponse (Error e) ->
        let updatedModel =
            {currentModel with State=ErrorState;ErrorMsg = TickPostFailed e.Message}
        updatedModel,Cmd.none 

let numPad (model:Model) (dispatch: Msg -> unit) =
    let stars =
        let length =
            match model.Pin with
            |Some p -> p.Length
            |None -> 0
        Array.init length (fun x -> "★")
        |> Array.fold (fun acc elem -> sprintf "%s%s" acc elem) ""
    let columns =
        [
            ["1"; "4"; "7";"Clear"]
            ["2"; "5"; "8"; "0"]
            ["3"; "6"; "9"; "Confirm"]
        ]
    let numPad = 
        columns
        |> List.map (fun numbers -> 
                        Column.column [Column.Width (Screen.Tablet,Column.IsOneQuarter)] 
                            [
                                for i in numbers do
                                    yield Container.container [Container.CustomClass "numPadContainer"] 
                                            [   Button.button 
                                                    [ 
                                                        if i = "Clear" then
                                                            yield! 
                                                                [
                                                                    Button.OnClick (fun ev -> ClearPin |> dispatch)
                                                                    Button.IsFullWidth
                                                                    Button.CustomClass "is-danger numPadBtn"]
                                                        elif i = "Confirm" then
                                                            yield! 
                                                                [
                                                                    Button.OnClick (fun ev ->   match model.Pin with
                                                                                                | Some p -> ConfirmPinRequest p |> dispatch
                                                                                                | _ -> GetBalanceResponse (Error (System.Exception("Pin not set"))) |> dispatch)
                                                                    Button.IsFullWidth
                                                                    Button.CustomClass "is-success numPadBtn"]
                                                        else
                                                            yield!
                                                                [   Button.OnClick (fun ev -> PinNumberAdd i|> dispatch)
                                                                    Button.IsFullWidth
                                                                    Button.CustomClass "is-info numPadBtn" ]
                                                    ]
                                                    [str i]
                                            ]
                            ])

    let accountDisplay = 
        Column.column [Column.Width (Screen.Tablet,Column.IsOneQuarter); Column.CustomClass "is-grey"] 
            [
                Container.container [Container.CustomClass "is-fullHeight numPadContainer"]
                    [
                        Image.image [Image.CustomClass "userImg";]
                            [img [Src "Images/safe_favicon.png"]]
                        Box.box' [Props [Class "box pinDisplay"] ]
                            [
                                p [Id "pinHead"] [str "PIN INPUT:"]
                                str stars
                            ]
                    ]
            ]

    Columns.columns [Columns.IsMobile;Columns.IsGapless;Columns.CustomClass "is-fullHeight"] 
        [yield! numPad; yield accountDisplay]                

let tickSelection (model : Model) (dispatch : Msg -> unit) =
    let coffeeTicker =
        Column.column [Column.Width (Screen.Tablet,Column.IsHalf)]
            [
                Box.box' [Props [OnClick (fun _ -> Tick (model.User.Value.Name,"Coffee",1,"") |> dispatch);Class "is-marginless"]]
                    [
                        Image.image [Image.IsSquare] 
                            [
                                img [Props.Src "Images/Coffee.png"]
                            ]
                    ]
                Container.container [Container.Props [Id "CoffeeText"]]
                    [
                        p [] [str "Tick Coffee"]
                    ]                
            ]
    let beerTicker =
        Column.column [Column.Width (Screen.Tablet,Column.IsHalf)]
            [
                Box.box' [Props [OnClick (fun _ -> BeverageSelectionWindow |> dispatch);Class "is-marginless"] ] 
                    [
                        Image.image [Image.IsSquare] 
                            [
                                img [Props.Src "Images/Beer.png"]                              
                            ]
                    ]
                Container.container [Container.Props [Id "BeerText"]]
                    [
                        p [] [str "Tick Beverage"]
                    ]                
            ]
    Columns.columns [Columns.IsMobile;Columns.IsGapless;Columns.CustomClass "is-fullHeight"] 
        [
            coffeeTicker
            beerTicker
        ]

let beverageSelection (amount:int) (model : Model) (dispatch : Msg -> unit) =
    Columns.columns [Columns.IsMobile;Columns.IsGapless;Columns.CustomClass "is-fullHeight is-lightBlue"] 
        [
            Column.column [Column.Width (Screen.Tablet,Column.IsOneFifth)] []
            Column.column [Column.Width (Screen.Tablet,Column.IsThreeFifths)] 
                [
                    Heading.h3 [Heading.CustomClass "has-text-centered topHeader"] [str "1. Select the amount of the beverage you want to tick:"]
                    Columns.columns [Columns.IsMobile;Columns.IsGapless;Columns.CustomClass"numPadContainer"]
                        [
                            Column.column [Column.Width (Screen.Tablet,Column.IsOneThird)] 
                                [
                                    Button.button [Button.IsFullWidth; Button.CustomClass "is-danger beverageBtn";Button.OnClick (fun _ ->  (document.getElementById "barcodeInput").focus()
                                                                                                                                            ChangeBeverageAmount (model.BeverageAmount - 1) |> dispatch)] 
                                        [
                                            Icon.icon [] [Fa.i [Fa.Solid.Minus] []]
                                        ]
                                ] 
                            Column.column [Column.Width (Screen.Tablet,Column.IsOneThird)] 
                                [
                                    Button.button [Button.IsFullWidth; Button.CustomClass "is-info beverageBtn"] 
                                        [
                                            str (string model.BeverageAmount)
                                        ]
                                ] 
                            Column.column [Column.Width (Screen.Tablet,Column.IsOneThird)] 
                                [
                                    Button.button [Button.IsFullWidth; Button.CustomClass "is-success beverageBtn"; Button.OnClick (fun _ ->(document.getElementById "barcodeInput").focus()
                                                                                                                                            ChangeBeverageAmount (model.BeverageAmount + 1) |> dispatch)] 
                                        [
                                            Icon.icon [] [Fa.i [Fa.Solid.Plus] [] ]
                                        ]
                                ] 
                        ]
                    Heading.h3 [Heading.CustomClass "has-text-centered"] [str "2. Scan a barcode to proceed:"]                    
                    Columns.columns [Columns.IsMobile;Columns.IsGapless;Columns.CustomClass"numPadContainer"] 
                        [
                            Column.column [Column.Width (Screen.Tablet,Column.IsHalf)] 
                                [
                                    Input.text [Input.CustomClass "is-marginless is-fullHeight"
                                                Input.Placeholder "Scan a barcode to proceed"
                                                Input.Id "barcodeInput"
                                                Input.Props [
                                                                AutoFocus true
                                                                OnKeyPress (fun x ->if x.key = "Enter" then  
                                                                                        console.log("Enter pressed")
                                                                                        let input = document.getElementById "barcodeInput"
                                                                                        let barcode = !!x.target?value
                                                                                        console.log(sprintf "Barcode WORKED!!! : %s"barcode)
                                                                                        Tick (model.User.Value.Name,"",model.BeverageAmount,barcode) |> dispatch
                                                                                    else
                                                                                        console.log(sprintf "differentKey pressed lol : %s" x.key))
                                                                ]
                                                ]
                                ]
                            Column.column [Column.Width (Screen.Tablet,Column.IsHalf)] 
                                [
                                    Button.button [Button.IsFullWidth;Button.CustomClass"is-large is-danger is-fullHeight";Button.OnClick (fun _ -> Reset |> dispatch)] [str "Abort"]
                                ]
                        ]
                ]
            Column.column [Column.Width (Screen.Tablet,Column.IsOneFifth)] []
        ]


let generateCardForBeverage name price imageSrc =
    Card.card [Props [Class "beverageCard"]] 
        [
            Image.image [] []
        ]


let errorMessage (msgHead:string) (msgBody:string) (model : Model) (dispatch : Msg -> unit)  =
    Message.message [Message.CustomClass "is-danger"] 
        [
            Message.header [] 
                [
                    p [] [str (sprintf "Error: %s" msgHead)]
                ]
            Message.body [CustomClass "full-message"] 
                [
                    div [] [str msgBody]
                    Button.button [Button.CustomClass "is-danger is-large";Button.OnClick (fun x -> Reset |> dispatch)] 
                        [str "Dismiss"]
                ]            
        ]

let tickPerformedMessage (amount: int) (tickedTrade: string) (userName:string) (model : Model) (dispatch : Msg -> unit) =
    let balance = sprintf "%.2f €"(- Math.Round(model.Balance,2))
    let balanceClass = if (- Math.Round(model.Balance,2)) < 0. then "is-danger" else "is-success"
    Message.message [Message.CustomClass "is-success"] 
        [
            Message.header [] 
                [
                    p [] [str "Tick successfull - Thank you"]
                ]
            Message.body [CustomClass "full-message"] 
                [
                    div [] 
                        [
                            p [] [str (sprintf "%i %s ticked for" amount tickedTrade)]
                            p [] [str userName]
                            Box.box' [Props [Class (sprintf "box-%s box" balanceClass)]] 
                                [
                                    p [] [str "current Balance:"]
                                    p [] [str balance]
                                ]
                        ]
                    Button.button [Button.CustomClass (sprintf "%s is-large" balanceClass);Button.OnClick (fun x -> Reset |> dispatch)] 
                        [str "OKAY"]
                ]            
        ]

let view (model : Model) (dispatch : Msg -> unit) =
    let display = 
        match model.State with
        |PinInput -> numPad
        |TickSelection -> tickSelection
        |BeverageSelection -> beverageSelection model.BeverageAmount
        |TickPerformed ->
            let name,amount,trade = model.TickedTrade
            tickPerformedMessage amount trade name
        |ErrorState -> 
            match model.ErrorMsg with
            |NoError -> 
                failwithf "unexpected error, set error state of model without updating the error message"
            |IncorrectPin msg -> 
                errorMessage "Pin confirmation failed" "The provided Pin is incorrect or does not exist"
            |TickPostFailed msg ->
                errorMessage "Tick post to server failed" "Barcode not recognized or server not available" 

    Container.container [Container.CustomClass "NumPadDiv"]
        [ 
            display model dispatch
        ]


#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
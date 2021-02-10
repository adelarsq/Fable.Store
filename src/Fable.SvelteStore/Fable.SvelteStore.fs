[<RequireQualifiedAccess>]
module SvelteStore

open System
open Fable
open Fable.Core
open ElmishStore

type Subscribe<'Value> = 'Value -> unit
type Dispose = delegate of unit -> unit
type IDispatcher<'Msg> = interface end

type IReadable<'Value> =
    abstract subscribe: Subscribe<'Value> -> Dispose

type IWritable<'Value> =
    inherit IReadable<'Value>
    abstract update: ('Value -> 'Value) -> unit
    abstract set: 'Value -> unit

type Initialize<'Props, 'Value> = delegate of 'Props -> IReadable<'Value>

[<Import("readable", from="svelte/store")>]
let private makeReadableStore (init: 'Value) (start: ('Value -> unit) -> Dispose): IReadable<'Value> = jsNative

[<Import("writable", from="svelte/store")>]
let private makeWritableStore (init: 'Value) (start: ('Value -> unit) -> Dispose): IWritable<'Value> = jsNative

let private storeCons value dispose =
    let mutable store = Unchecked.defaultof<IWritable<'Value>>
    store <- makeWritableStore value (fun _set ->
        Dispose(fun () -> store.update(fun model ->
            dispose model
            model)))
    store, store.update

let make init dispose props: IWritable<'Model> =
    Store.makeWithCons init dispose storeCons props

let makeRec (init: IWritable<'Model> -> 'Props -> 'Model * IDisposable) =
    fun (props: 'Props) ->
        let mutable store = Unchecked.defaultof<IWritable<'Model>>
        store <- makeWritableStore Unchecked.defaultof<'Model> (fun set ->
            let v, disp = init store props
            set v
            Dispose(fun () -> disp.Dispose()))
        store

let makeElmish (init: 'Props -> 'Value * Cmd<'Value, 'Msg>)
               (update: 'Msg -> 'Value -> 'Value * Cmd<'Value, 'Msg>)
               (dispose: 'Value -> unit)
               (props: 'Props): IWritable<'Value> * Dispatch<'Msg> =

    Store.makeElmishWithCons init update dispose storeCons props

let makeElmishSimple (init: 'Props -> 'Value)
                     (update: 'Msg -> 'Value -> 'Value)
                     (dispose: 'Value -> unit)
                     (props: 'Props): IWritable<'Value> * Dispatch<'Msg> =

    let init p = init p, []
    let update m v = update m v, []
    makeElmish init update dispose props

[<SveltePlugins.Dispatcher>]
let makeDispatcher (dispatch: 'Msg -> unit): IDispatcher<'Msg> = failwith "never"

let map (f: 'a -> 'b) (store: IReadable<'a>): IReadable<'b> =
    makeReadableStore Unchecked.defaultof<_> (fun set ->
        let disp = store.subscribe(f >> set)
        Dispose(fun () -> disp.Invoke()))

import { IInputs, IOutputs } from "./generated/ManifestTypes";

export class DeviceConditionRating implements ComponentFramework.StandardControl<IInputs, IOutputs> {
    // Add properties for the control
    private _value: number;
    private _notifyOutputChanged: () => void;
    private _container: HTMLDivElement;
    private _ratingInput: HTMLInputElement;
    private _ratingLabel: HTMLLabelElement;
    private _onRatingChanged: EventListenerOrEventListenerObject;

    /**
     * Empty constructor.
     */
    constructor() {
        // Empty
    }

    /**
     * Used to initialize the control instance. Controls can kick off remote server calls and other initialization actions here.
     * Data-set values are not initialized here, use updateView.
     * @param context The entire property bag available to control via Context Object; It contains values as set up by the customizer mapped to property names defined in the manifest, as well as utility functions.
     * @param notifyOutputChanged A callback method to alert the framework that the control has new outputs ready to be retrieved asynchronously.
     * @param state A piece of data that persists in one session for a single user. Can be set at any point in a controls life cycle by calling 'setControlState' in the Mode interface.
     * @param container If a control is marked control-type='standard', it will receive an empty div element within which it can render its content.
     */
    public init(
        context: ComponentFramework.Context<IInputs>,
        notifyOutputChanged: () => void,
        state: ComponentFramework.Dictionary,
        container: HTMLDivElement
    ): void {
        console.log("DeviceConditionRating: init");

        // Add control initialization code
        this._notifyOutputChanged = notifyOutputChanged;
        this._onRatingChanged = this.onRatingChanged.bind(this);

        this._container = document.createElement("div");
        this._container.setAttribute("class", "eqp-rating-container");

        this._ratingInput = document.createElement("input");
        this._ratingInput.setAttribute("type", "range");
        this._ratingInput.setAttribute("min", "1");
        this._ratingInput.setAttribute("max", "5");
        this._ratingInput.setAttribute("step", "1");
        this._ratingInput.setAttribute("class", "eqp-rating-slider");
        this._ratingInput.addEventListener("input", this._onRatingChanged);

        this._ratingLabel = document.createElement("label");
        this._ratingLabel.setAttribute("class", "eqp-rating-label");

        this._value = context.parameters.rating.raw ?? 1;
        this._ratingInput.value = this._value.toString();
        this._ratingLabel.innerHTML = this._value.toString();

        this._container.appendChild(this._ratingInput);
        this._container.appendChild(this._ratingLabel);
        container.appendChild(this._container);
    }

    /**
     * Event handler for the rating slider's input event. Updates the label and notifies the
     * framework that a new output value is ready to be retrieved through getOutputs.
     * @param evt The input event raised by the rating slider.
     */
    public onRatingChanged(evt: Event): void {
        this._value = parseInt(this._ratingInput.value, 10);
        this._ratingLabel.innerHTML = this._value.toString();
        this._notifyOutputChanged();
    }

    /**
     * Called when any value in the property bag has changed. This includes field values, data-sets, global values such as container height and width, offline status, control metadata values such as label, visible, etc.
     * @param context The entire property bag available to control via Context Object; It contains values as set up by the customizer mapped to names defined in the manifest, as well as utility functions
     */
    public updateView(context: ComponentFramework.Context<IInputs>): void {
        console.log("DeviceConditionRating: updateView");

        // Add code to update control view
        this._value = context.parameters.rating.raw ?? this._value;
        this._ratingInput.value = this._value.toString();
        this._ratingLabel.innerHTML = this._value.toString();
    }

    /**
     * It is called by the framework prior to a control receiving new data.
     * @returns an object based on nomenclature defined in manifest, expecting object[s] for property marked as "bound" or "output"
     */
    public getOutputs(): IOutputs {
        console.log("DeviceConditionRating: getOutputs");

        // Add code to report the changed rating
        return {
            rating: this._value
        };
    }

    /**
     * Called when the control is to be removed from the DOM tree. Controls should use this call for cleanup.
     * i.e. cancelling any pending remote calls, removing listeners, etc.
     */
    public destroy(): void {
        console.log("DeviceConditionRating: destroy");

        // Add code to cleanup control if necessary
        this._ratingInput.removeEventListener("input", this._onRatingChanged);
    }
}

import "Sundex.Style.DSL/Examples/default.snx.ss";

// here when specifying a component with a "@" prefix, we fully override the default imported one.
// when it's not specified, values that aren't specified in the new definition will be inherited from the old definition.


// this fully overrides the default imported component
@component button {
    background = "#ff0000";
    color = "#ffffff";
    padding = 20px;
}


// if label is also defined in the imported file, this will import the fields that aren't overriden in the new definition
component label {
    font-color = "#000000";
    background = "#ffffff";
}
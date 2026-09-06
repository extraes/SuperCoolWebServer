import { withErrorHandling, blinkDsButton, removeDsColors } from "../frontend/js/Utils.js";

export class Links {
    static extraHeaders = {};
    
    static setLink() {
        return withErrorHandling(() => this.setLinkImpl(), $("#set-link-result"), $("#set-link-btn"));
    }
    
    static async setLinkImpl() {
        let name = $("#set-link-name");
        let target = $("#set-link-target")
        let output = $("#set-link-output");
        let btn = $("#set-link-btn");
        
        let linkComponent= encodeURIComponent(name.val());
        let targetComponent = encodeURIComponent(target.val());
        let result = await fetch(`/links/set/${linkComponent}?target=${targetComponent}`, {
            method: "PUT",
            headers: this.extraHeaders,
        });
        
        removeDsColors(btn);
        output.show();
        output.parent().show();
        if (result.ok) {
            output.text(`Successfully set link!`)
            btn.addClass("ds-green")
        }
        else {
            blinkDsButton(btn);
            output.text(`HTTP error ${result.status} (${result.statusText}): ${await result.text()}`)
        }
    }
}

window.Links = Links;
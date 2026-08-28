import { withErrorHandling } from "/frontend/js/Utils.js";


export class Files {
    static oldestFirst = false;
    static extraHeaders = {};
    
    static toggleReverse() {
        this.oldestFirst = !this.oldestFirst;
        
        let btn = $("#file-list-reverse")
        
        if (this.oldestFirst) {
            btn.addClass("ds-green");
            btn.removeClass("ds-red");
        }
        else {
            btn.removeClass("ds-green");
            btn.addClass("ds-red");
        }
    }
    
    static getFiles() {
        return withErrorHandling(() => this.getFilesImpl(), $("#file-list-err"));
    }
    
    static async getFilesImpl() {
        let errElement = $("#file-list-err");
        let resultElement = $("#file-list-results");
        let filter = $("#file-list-filter");
        let filterStr = filter.val()?.trim() || "*";

        let reqHeaders = { };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);


        errElement.empty().hide();
        resultElement.empty().hide();
        let result = await fetch(`/api/files/list/${encodeURIComponent(filterStr)}?oldestFirst=${this.oldestFirst}`, {
            method: "GET",
            headers: reqHeaders
        });

        if (result.ok)
        {
            let json = await result.json();
            let items = json["items"];
            let total = json["total"];

            let statusText = `${total} file(s)`;
            if (items.length < total) {
                statusText += " (Narrow your search to see other files)"
            }
            
            $("<div>")
                .addClass("pictochat-status")
                .text(statusText)
                .appendTo(resultElement);
            for (let fileName of items) {
                $("<div>")
                    .addClass("pictochat-message")
                    .text(fileName)
                    .appendTo(resultElement);
            }
            
            resultElement.show();
        }
        else
        {
            errElement.text(`HTTP ${result.status} (${result.statusText}) error: ${await result.text()}`)
            
            errElement.show();
        }
    }
}

window.Files = Files;

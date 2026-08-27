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
    
    static async getFiles() {
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

            // resultElement.add("div").addClass("pictochat-status").text(`${json.length} file(s)`);
            $("<div>")
                .addClass("pictochat-status")
                .text(`${json.length} file(s)`)
                .appendTo(resultElement);
            for (let fileName of json) {
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
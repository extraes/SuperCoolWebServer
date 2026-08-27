export class Files {
    static recentFirst = true;
    
    static toggleReverse() {
        this.recentFirst = !this.recentFirst;
        
        let btn = $("#file-list-reverse")
        
        if (this.recentFirst) {
            btn.addClass("ds-green");
            btn.removeClass("ds-red");
        }
        else {
            btn.removeClass("ds-green");
            btn.addClass("ds-red");
        }
    }
    
    static getFiles() {
        let filter = $("#file-list-filter");
        let filterStr = encodeURIComponent(filter.val())
    }
  
}

window.Files = Files;
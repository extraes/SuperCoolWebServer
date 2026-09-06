export async function withErrorHandling(operation, errorTextElement, errorBtn) {
    try {
        return await operation()
    }
    catch(err) {
        console.error(err);

        errorTextElement.parent().show();

        errorTextElement.text(`Error: ${err}`).show();

        if (errorBtn)
            blinkDsButton(errorBtn);
        
        return undefined;
    }
}


export function blinkDsButton(buttonElement) {
    // Blink the button in the stuuupidest way possible
    for (let i = 0; i < 10; i++) {
        if (i % 2 === 0)
            setTimeout(() => { buttonElement.addClass("ds-red") }, i * 250);
        else
            setTimeout(() => { buttonElement.removeClass("ds-red") }, i * 250);
    }
}

export function removeDsColors(element) {
    // Source - https://stackoverflow.com/a/1227309
    // Posted by redsquare, modified by community. See post 'Timeline' for change history
    // Retrieved 2026-08-28, License - CC BY-SA 4.0

    // noinspection SpellCheckingInspection
    let colors = [
        "ds-slate",
        "ds-maroon",
        "ds-red",
        "ds-pink",
        "ds-orange",
        "ds-yellow",
        "ds-neonyellow",
        "ds-lime",
        "ds-green",
        "ds-teal",
        "ds-turquoise",
        "ds-blue",
        "ds-navy",
        "ds-darkpurple",
        "ds-magenta",
        "ds-fuschia",
    ];
    for (const color of colors) {
        element.removeClass(color);
        element.removeClass(color + "-50");
    }
}
export async function withErrorHandling(operation, errorElement) {
    try {
        return await operation()
    }
    catch(err) {
        console.error(err);

        errorElement.parent().show();

        errorElement.text(`Error: ${err}`).show();

        return undefined;
    }
}

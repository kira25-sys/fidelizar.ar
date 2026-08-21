// The two <dialog> calls Blazor has no managed equivalent for. showModal() is what gives the
// native focus trap, the Esc handling and the inert backdrop (DESIGN-SYSTEM §9.2).

export function abrirModal(dialogo) {
    if (dialogo && !dialogo.open) {
        dialogo.showModal();
    }
}

export function cerrar(dialogo) {
    if (dialogo && dialogo.open) {
        dialogo.close();
    }
}

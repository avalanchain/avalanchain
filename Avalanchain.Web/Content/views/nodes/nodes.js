(function () {
    'use strict';
    var controllerId = 'Nodes';
    angular.module('avalanchain').controller(controllerId, ['common', Nodes]);
    function Nodes(common) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'Nodes';
        this.helloText = 'Welcome in Avalanchain';
        this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated Nodes') });//log('Activated Admin View');
        }
    };


})();
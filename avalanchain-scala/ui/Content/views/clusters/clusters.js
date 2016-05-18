(function () {
    'use strict';
    var controllerId = 'Clusters';
    angular.module('avalanchain').controller(controllerId, ['common', clusters]);

    function clusters(common) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'Clusters';
        this.helloText = 'Welcome in Avalanchain';
        this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated Clusters') });//log('Activated Admin View');
        }
    };


})();
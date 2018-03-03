(function () {
    'use strict';
    var controllerId = 'Doctor';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', doctor]);

    function doctor(common, dataservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'Doctor Page';
        this.helloText = 'Welcome in avalanchain';
        this.descriptionText = 'PATIENT DESCRIPTION';
        this.patientsCount = 15000;
        this.photoCount = 101000;

        this.photoLinks = ['https://conceitedcrusade.files.wordpress.com/2015/09/xray-broken-arm2.jpg', 'http://farm6.static.flickr.com/5257/5443372696_9f847dbd4e.jpg', 'http://jeeblogdotcom.files.wordpress.com/2011/12/broken-arm-xray.jpg', 'https://s-media-cache-ak0.pinimg.com/736x/9e/04/1c/9e041c55e890991a147458dc2ae16f2d.jpg', 'http://www.chla.org/sites/default/files/migrated/x-ray-arm.jpg', 'http://www.tssphoto.com/medical/WMED1401.jpg', 'http://images.emedicinehealth.com/images/4453/4453-4463-8895-24990tn.jpg', 'http://images.emedicinehealth.com/images/4453/4453-4463-8895-24989tn.jpg'];
        this.photos = [];
        var k = 0;
        for (var j = 0; j < 150 ; j++) {
            if (k > 7) k = 0;
            this.photos.push(this.photoLinks[k]);
            k++;
        }
        
        this.hideShowPatPhot = function (value) {
            this.val = !!value;
        };
        this.valinside = false;
        this.showPatDecript = function (value) {
            this.current = value;
            this.valinside = true;
        };

        this.loadPatiens = function () {
            
        };
        var num = 1;
        var patients = [];
        for (var i = 0; i < this.patientsCount; i++) {
            var photos = [];
            if (num > 7) num = 1;
            for (var j = 0; j < Math.floor(Math.random() * 8) + 1  ; j++) {
                photos.push(this.photoLinks[j]);
            }
            patients.push({
                id: i + 1,
                icon: '/Content/img/' + num + '.png',
                photos: photos,
                name: 'Patient_' + i,
                doctor: 'Abrams J'
            });
            num++;
        }
        this.patients = patients;
        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated Doctor View') });//log('Activated Admin View');
        }
    };


})();
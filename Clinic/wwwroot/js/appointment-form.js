// Shared behavior for the appointment create/edit forms:
// - loads available time slots for the selected doctor + date + duration
// - searches/selects a patient
// - creates a patient inline via the "New Patient" modal
// The page must contain: #DoctorId, #AppointmentDate (hidden yyyy-MM-dd),
// #DurationMinutes, #StartTime (hidden), #slotContainer, #slotError,
// #patientSearch, #searchPatientBtn, #patientResults, #selectedPatientName,
// #PatientId, #newPatientBtn and the #patientCreateModal partial.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var doctorSelect = document.getElementById('DoctorId');
        var dateInput = document.getElementById('AppointmentDate');
        var durationInput = document.getElementById('DurationMinutes');
        var slotContainer = document.getElementById('slotContainer');
        var slotError = document.getElementById('slotError');
        var startTimeInput = document.getElementById('StartTime');
        var selectedSlot = null;

        if (!doctorSelect || !dateInput || !slotContainer) return;

        function loadSlots() {
            var doctorId = doctorSelect.value;
            var date = dateInput.value;
            var duration = durationInput ? durationInput.value : '30';

            selectedSlot = null;
            updateSlotSummary(null);

            if (!doctorId || !date) {
                slotContainer.innerHTML = '<span class="text-muted">Select a doctor and a date to see available times.</span>';
                return;
            }

            var url = '/Appointment/AvailableSlots?doctorId=' + encodeURIComponent(doctorId)
                + '&date=' + encodeURIComponent(date)
                + '&durationMinutes=' + encodeURIComponent(duration);

            fetch(url, {
                headers: { 'Accept': 'application/json' }
            })
                .then(function (response) {
                    if (!response.ok) throw new Error('Failed to load slots.');
                    return response.json();
                })
                .then(function (response) {
                    slotError.textContent = '';
                    if (response.doctorNotFound) {
                        slotContainer.innerHTML = '<span class="text-muted">The selected doctor no longer exists.</span>';
                        return;
                    }
                    renderSlots(response);
                })
                .catch(function () {
                    slotError.textContent = 'Could not load available times.';
                    slotContainer.innerHTML = '';
                });
        }

        function renderSlots(response) {
            slotContainer.innerHTML = '';

            var available = (response.slots || []).filter(function (slot) { return slot.isAvailable; });

            if (available.length === 0) {
                slotContainer.innerHTML = response.message
                    ? '<span class="text-muted">' + response.message + '</span>'
                    : '<span class="text-muted">No available times for this doctor and date.</span>';
                return;
            }

            // Quick pick for the first still-available start time.
            if (response.nextAvailable) {
                var nextButton = document.createElement('button');
                nextButton.type = 'button';
                nextButton.className = 'btn btn-sm btn-outline-success mb-1';
                nextButton.textContent = 'Next available - ' + response.nextAvailable;
                nextButton.addEventListener('click', function () {
                    var match = slotContainer.querySelector('button[data-start-time="' + response.nextAvailable + '"]');
                    if (match) selectSlot(match);
                });
                slotContainer.appendChild(nextButton);
            }

            available.forEach(function (slot) {
                var start = slot.startTime.slice(0, 5);
                var label = start + ' - ' + slot.endTime.slice(0, 5);
                var button = document.createElement('button');
                button.type = 'button';
                button.className = 'btn btn-outline-primary btn-slot';
                button.textContent = start;
                button.title = label;
                button.dataset.startTime = start;
                button.dataset.endTime = slot.endTime.slice(0, 5);
                button.addEventListener('click', function () {
                    selectSlot(button);
                });
                slotContainer.appendChild(button);
            });

            // Read-only daily overview for the same selection.
            var link = document.createElement('a');
            link.className = 'btn btn-sm btn-link';
            link.textContent = 'View daily schedule';
            link.href = '/Appointment/DailySchedule?doctorId=' + encodeURIComponent(doctorSelect.value)
                + '&date=' + encodeURIComponent(dateInput.value)
                + '&durationMinutes=' + encodeURIComponent(durationInput ? durationInput.value : '30');
            slotContainer.appendChild(link);

            // On edit, highlight the appointment's current start time. The hidden
            // input may render with seconds, so compare only the first 5 chars.
            if (startTimeInput.value) {
                var currentValue = startTimeInput.value.slice(0, 5);
                var current = slotContainer.querySelector('button[data-start-time="' + currentValue + '"]');
                if (current) selectSlot(current);
            }
        }

        function selectSlot(button) {
            if (selectedSlot) {
                selectedSlot.classList.remove('btn-primary');
                selectedSlot.classList.add('btn-outline-primary');
            }
            selectedSlot = button;
            button.classList.remove('btn-outline-primary');
            button.classList.add('btn-primary');
            startTimeInput.value = button.dataset.startTime;
            updateSlotSummary(button);
        }

        function updateSlotSummary(button) {
            var summary = document.getElementById('slotSummary');
            if (!summary) return;

            if (!button || !button.dataset.startTime) {
                summary.textContent = '';
                return;
            }

            var doctorText = '';
            if (doctorSelect.selectedOptions && doctorSelect.selectedOptions.length > 0) {
                doctorText = doctorSelect.selectedOptions[0].textContent.split('\u2014')[0].trim();
            }

            var parts = [];
            if (doctorText) parts.push(doctorText);
            if (dateInput.value) parts.push(dateInput.value);
            if (button.dataset.startTime) {
                parts.push(button.dataset.startTime + ' - ' + (button.dataset.endTime || ''));
            }
            if (durationInput) parts.push(durationInput.value + ' minutes');

            summary.textContent = parts.join(' | ');
        }

        doctorSelect.addEventListener('change', function () { loadSlots(); loadDailySchedule(); });
        dateInput.addEventListener('change', function () { loadSlots(); loadDailySchedule(); });
        if (durationInput) {
            durationInput.addEventListener('change', function () {
                selectedSlot = null;
                loadSlots();
                loadDailySchedule();
            });
        }

        // Daily schedule preview for the selected doctor + date (Create page).
        var dailyScheduleContainer = document.getElementById('dailyScheduleContainer');

        function loadDailySchedule() {
            if (!dailyScheduleContainer) return;

            var doctorId = doctorSelect.value;
            var date = dateInput.value;
            var duration = durationInput ? durationInput.value : '30';

            if (!doctorId || !date) {
                dailyScheduleContainer.innerHTML = '<span class="text-muted">Select a doctor and a date to see the daily schedule.</span>';
                return;
            }

            var url = '/Appointment/DailyScheduleData?doctorId=' + encodeURIComponent(doctorId)
                + '&date=' + encodeURIComponent(date)
                + '&durationMinutes=' + encodeURIComponent(duration);

            fetch(url, {
                headers: { 'Accept': 'application/json' }
            })
                .then(function (response) { return response.json(); })
                .then(function (data) { renderDailySchedule(data); })
                .catch(function () {
                    dailyScheduleContainer.innerHTML = '<span class="text-muted">Could not load the daily schedule.</span>';
                });
        }

        function renderDailySchedule(data) {
            if (!dailyScheduleContainer) return;

            if (data.doctorNotFound) {
                dailyScheduleContainer.innerHTML = '<span class="text-muted">The selected doctor no longer exists.</span>';
                return;
            }

            var html = '<div class="fw-semibold">' + data.doctorName + ' \u2014 ' + data.date + '</div>';

            if (!data.isWorking) {
                html += '<span class="text-muted">' + (data.message || 'Doctor is not working on this date.') + '</span>';
                dailyScheduleContainer.innerHTML = html;
                return;
            }

            if (data.workingPeriods.length > 0) {
                html += '<div class="form-text">Working hours: '
                    + data.workingPeriods.map(function (p) { return p.start + ' \u2013 ' + p.end; }).join(', ')
                    + '</div>';
            }

            if (data.appointments.length > 0) {
                html += '<table class="table table-sm table-striped table-hover mt-2 mb-2">'
                    + '<thead><tr><th>Time</th><th>Patient</th><th>Status</th></tr></thead><tbody>';

                data.appointments.forEach(function (appt) {
                    var badge = appt.status === 'Completed'
                        ? 'text-bg-success'
                        : (appt.status === 'Scheduled' ? 'text-bg-primary' : 'text-bg-secondary');
                    html += '<tr>'
                        + '<td>' + appt.start + ' \u2013 ' + appt.end + '</td>'
                        + '<td>' + appt.patient + '</td>'
                        + '<td><span class="badge ' + badge + '">' + appt.status + '</span></td>'
                        + '</tr>';
                });

                html += '</tbody></table>';
            } else {
                html += '<p class="text-muted mb-1">' + (data.message || 'No appointments scheduled.') + '</p>';
            }

            if (data.availableStarts.length > 0) {
                html += '<div class="form-text">Available: '
                    + data.availableStarts.map(function (s) { return '<span class="badge text-bg-success">' + s + '</span>'; }).join(' ')
                    + '</div>';
            }

            dailyScheduleContainer.innerHTML = html;
        }

        // Patient search
        var searchBtn = document.getElementById('searchPatientBtn');
        var searchInput = document.getElementById('patientSearch');
        var resultsContainer = document.getElementById('patientResults');
        var selectedPatientName = document.getElementById('selectedPatientName');
        var patientIdInput = document.getElementById('PatientId');

        if (searchBtn && searchInput && resultsContainer && selectedPatientName && patientIdInput) {
            function searchPatients() {
                var term = searchInput.value.trim();
                fetch('/Patient/SearchAjax?term=' + encodeURIComponent(term), {
                    headers: { 'Accept': 'application/json' }
                })
                    .then(function (response) { return response.json(); })
                    .then(function (patients) {
                        resultsContainer.innerHTML = '';

                        if (patients.length === 0) {
                            resultsContainer.innerHTML = '<span class="text-muted">No patients found.</span>';
                            return;
                        }

                        var table = document.createElement('table');
                        table.className = 'table table-sm table-bordered';
                        table.innerHTML = '<thead><tr><th>Name</th><th>Phone</th><th></th></tr></thead>';
                        var tbody = document.createElement('tbody');

                        patients.forEach(function (patient) {
                            var row = document.createElement('tr');
                            row.innerHTML = '<td>' + patient.name + '</td><td>' + patient.phone + '</td>';

                            var cell = document.createElement('td');
                            var button = document.createElement('button');
                            button.type = 'button';
                            button.className = 'btn btn-sm btn-outline-primary';
                            button.textContent = 'Select';
                            button.addEventListener('click', function () {
                                patientIdInput.value = patient.patientId;
                                selectedPatientName.textContent = 'Selected: ' + patient.name + ' (' + patient.phone + ')';
                                resultsContainer.innerHTML = '';
                            });
                            cell.appendChild(button);
                            row.appendChild(cell);
                            tbody.appendChild(row);
                        });

                        table.appendChild(tbody);
                        resultsContainer.appendChild(table);
                    });
            }

            searchBtn.addEventListener('click', function (event) {
                event.preventDefault();
                searchPatients();
            });

            searchInput.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    searchPatients();
                }
            });
        }

        // Inline patient creation
        var patientCreateForm = document.getElementById('patientCreateForm');
        var patientCreateErrors = document.getElementById('patientCreateErrors');
        var patientCreateModal = document.getElementById('patientCreateModal');
        var patientFieldIds = {
            Name: 'newPatientName',
            BirthDate: 'newPatientBirthDate',
            Gender: 'newPatientGender',
            Phone: 'newPatientPhone'
        };

        function clearPatientFieldErrors() {
            Object.keys(patientFieldIds).forEach(function (key) {
                var input = document.getElementById(patientFieldIds[key]);
                var feedback = document.getElementById(patientFieldIds[key] + 'Error');
                if (input) input.classList.remove('is-invalid');
                if (feedback) feedback.textContent = '';
            });
            patientCreateErrors.textContent = '';
            patientCreateErrors.classList.add('d-none');
        }

        // Renders the { field: [messages] } errors returned by Patient/CreateAjax
        // beside the matching fields; anything not tied to a field goes in the
        // general alert. Entered values are left untouched so the secretary can
        // correct the form.
        function showPatientCreateErrors(errors) {
            clearPatientFieldErrors();
            var genericMessages = [];

            Object.keys(errors || {}).forEach(function (key) {
                var messages = errors[key] || [];
                var inputId = patientFieldIds[key];
                var input = inputId ? document.getElementById(inputId) : null;

                if (input) {
                    input.classList.add('is-invalid');
                    var feedback = document.getElementById(inputId + 'Error');
                    if (feedback) feedback.textContent = messages.join(' ');
                } else {
                    genericMessages = genericMessages.concat(messages);
                }
            });

            if (genericMessages.length > 0) {
                patientCreateErrors.textContent = genericMessages.join(' ');
                patientCreateErrors.classList.remove('d-none');
            }
        }

        if (patientCreateForm && patientCreateErrors && patientCreateModal) {
            patientCreateForm.addEventListener('submit', function (event) {
                event.preventDefault();

                var tokenInput = patientCreateForm.querySelector('input[name="__RequestVerificationToken"]');
                var payload = {
                    Name: document.getElementById('newPatientName').value,
                    BirthDate: document.getElementById('newPatientBirthDate').value,
                    Gender: document.getElementById('newPatientGender').value,
                    Phone: document.getElementById('newPatientPhone').value
                };

                var headers = {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                };
                if (tokenInput) headers['RequestVerificationToken'] = tokenInput.value;

                fetch('/Patient/CreateAjax', {
                    method: 'POST',
                    headers: headers,
                    body: JSON.stringify(payload)
                })
                    .then(function (response) { return response.json(); })
                    .then(function (result) {
                        if (result.success) {
                            patientIdInput.value = result.patientId;
                            selectedPatientName.textContent = 'Selected: ' + result.name + ' (' + result.phone + ')';
                            searchInput.value = result.name;
                            patientCreateForm.reset();
                            clearPatientFieldErrors();

                            var modal = bootstrap.Modal.getInstance(patientCreateModal);
                            if (modal) modal.hide();
                        } else {
                            showPatientCreateErrors(result.errors);
                        }
                    })
                    .catch(function () {
                        patientCreateErrors.textContent = 'Could not save the patient.';
                        patientCreateErrors.classList.remove('d-none');
                    });
            });

            patientCreateModal.addEventListener('show.bs.modal', clearPatientFieldErrors);
            patientCreateModal.addEventListener('hidden.bs.modal', function () {
                patientCreateForm.reset();
                clearPatientFieldErrors();
            });
        }

        // Load slots immediately when a doctor and date are already present (edit form).
        loadSlots();
        loadDailySchedule();
    });
})();
